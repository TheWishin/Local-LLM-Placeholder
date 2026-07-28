using System.Text;
using System.Text.Json.Nodes;
using DataAnonymizer.Services;

namespace DataAnonymizer.Proxy;

/// <summary>
/// Übersetzt einen gestreamten Anthropic-SSE-Antwortstrom in Echtzeit zurück:
/// Platzhalter in den Text-Deltas (und in gestreamten Tool-Argumenten) werden
/// wieder durch die Originalwerte ersetzt, während die Daten durchfliessen.
///
/// Schwierigkeit: ein Platzhalter wie <c>[NAME_1]</c> kann über zwei Chunks
/// verteilt ankommen (<c>[NA</c> … <c>ME_1]</c>). Deshalb wird pro Inhaltsblock
/// ein möglicher unvollständiger Platzhalter am Ende zurückgehalten (ab der
/// letzten nicht geschlossenen <c>[</c>) und erst emittiert, wenn er vollständig
/// ist – spätestens beim <c>content_block_stop</c>.
///
/// Verarbeitet wird ereignisweise: erst wenn ein SSE-Ereignis komplett ist
/// (Leerzeile), wird es – ggf. mit vorangestelltem Rest – ausgegeben.
/// </summary>
public sealed class SseDeanonymizer
{
    // Zwei einmal gebaute Rückübersetzer: einer für reinen Text, einer für
    // JSON-Fragmente (dort werden die Originalwerte JSON-escaped eingesetzt).
    private readonly Func<string, string> _deanon;
    private readonly Func<string, string> _deanonJson;

    private readonly StringBuilder _lineCarry = new();      // unvollständige Zeile über Chunk-Grenzen
    private readonly List<string> _eventLines = new();       // Zeilen des aktuellen Ereignisses
    private readonly Dictionary<int, string> _pendingText = new();
    private readonly Dictionary<int, string> _pendingJson = new();

    public SseDeanonymizer(IReadOnlyList<MappingEntry> mappings, AnonymizerService service)
    {
        _deanon = service.BuildDeanonymizer(mappings);
        _deanonJson = service.BuildDeanonymizer(mappings, AnthropicRewriter.JsonEscapeInner);
    }

    /// <summary>Verarbeitet ein weiteres Stück des Streams und liefert den umgeschriebenen Teil.</summary>
    public string Push(string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return string.Empty;
        }

        _lineCarry.Append(chunk);
        var buffered = _lineCarry.ToString();
        var output = new StringBuilder();

        int start = 0;
        int nl;
        while ((nl = buffered.IndexOf('\n', start)) >= 0)
        {
            var line = buffered[start..nl];          // ohne \n
            start = nl + 1;
            HandleLine(line, output);
        }

        _lineCarry.Clear();
        _lineCarry.Append(buffered[start..]);        // Rest ohne \n aufheben
        return output.ToString();
    }

    /// <summary>Schliesst den Stream ab: letztes Ereignis und zurückgehaltene Reste ausgeben.</summary>
    public string Complete()
    {
        var output = new StringBuilder();
        if (_lineCarry.Length > 0)
        {
            HandleLine(_lineCarry.ToString(), output);
            _lineCarry.Clear();
        }
        if (_eventLines.Count > 0)
        {
            EmitEvent(_eventLines, output);
            _eventLines.Clear();
        }
        return output.ToString();
    }

    private void HandleLine(string line, StringBuilder output)
    {
        // \r am Zeilenende (CRLF) erhalten wir separat; für die Leerzeilen-Erkennung ignorieren.
        var isBlank = line.Length == 0 || line == "\r";
        if (isBlank)
        {
            EmitEvent(_eventLines, output);
            _eventLines.Clear();
            output.Append(line).Append('\n');       // Leerzeile als Ereignistrenner behalten
            return;
        }
        _eventLines.Add(line);
    }

    private void EmitEvent(List<string> lines, StringBuilder output)
    {
        if (lines.Count == 0)
        {
            return;
        }

        // Ereignistyp aus der data:-Zeile bestimmen.
        var (dataIndex, payload) = FindData(lines);
        if (payload is null)
        {
            // Kein data:-JSON – unverändert ausgeben.
            foreach (var l in lines)
            {
                output.Append(l).Append('\n');
            }
            return;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(payload);
        }
        catch
        {
            node = null;
        }

        if (node is JsonObject obj)
        {
            var type = GetString(obj, "type");
            if (type == "content_block_start")
            {
                var idx = GetInt(obj, "index") ?? 0;
                _pendingText.Remove(idx);
                _pendingJson.Remove(idx);
            }
            else if (type == "content_block_delta")
            {
                RewriteDelta(obj);
            }
            else if (type == "content_block_stop")
            {
                var idx = GetInt(obj, "index") ?? 0;
                // Zurückgehaltenen Rest vor dem Stop-Ereignis nachliefern.
                EmitFlush(idx, output);
            }

            var rewritten = obj.ToJsonString();
            for (var i = 0; i < lines.Count; i++)
            {
                if (i == dataIndex)
                {
                    output.Append("data: ").Append(rewritten).Append('\n');
                }
                else
                {
                    output.Append(lines[i]).Append('\n');
                }
            }
            return;
        }

        foreach (var l in lines)
        {
            output.Append(l).Append('\n');
        }
    }

    private void RewriteDelta(JsonObject obj)
    {
        var idx = GetInt(obj, "index") ?? 0;
        if (obj["delta"] is not JsonObject delta)
        {
            return;
        }
        var dtype = GetString(delta, "type");

        if (dtype == "text_delta")
        {
            var current = _pendingText.GetValueOrDefault(idx, string.Empty) + (GetString(delta, "text") ?? string.Empty);
            var (emit, keep) = SplitHoldback(current);
            _pendingText[idx] = keep;
            delta["text"] = _deanon(emit);
        }
        else if (dtype == "input_json_delta")
        {
            var current = _pendingJson.GetValueOrDefault(idx, string.Empty) + (GetString(delta, "partial_json") ?? string.Empty);
            var (emit, keep) = SplitHoldback(current);
            _pendingJson[idx] = keep;
            // Innerhalb eines JSON-Fragments müssen die Originalwerte JSON-escaped werden.
            delta["partial_json"] = _deanonJson(emit);
        }
    }

    private void EmitFlush(int idx, StringBuilder output)
    {
        if (_pendingText.TryGetValue(idx, out var pt) && pt.Length > 0)
        {
            EmitSyntheticDelta(idx, "text_delta", "text", _deanon(pt), output);
            _pendingText[idx] = string.Empty;
        }
        if (_pendingJson.TryGetValue(idx, out var pj) && pj.Length > 0)
        {
            EmitSyntheticDelta(idx, "input_json_delta", "partial_json", _deanonJson(pj), output);
            _pendingJson[idx] = string.Empty;
        }
    }

    private static void EmitSyntheticDelta(int idx, string deltaType, string field, string value, StringBuilder output)
    {
        var delta = new JsonObject { ["type"] = deltaType, [field] = value };
        var evt = new JsonObject { ["type"] = "content_block_delta", ["index"] = idx, ["delta"] = delta };
        output.Append("event: content_block_delta\n");
        output.Append("data: ").Append(evt.ToJsonString()).Append('\n');
        output.Append('\n');
    }

    /// <summary>Teilt Text in (ausgebbar, zurückhalten): ab der letzten nicht geschlossenen <c>[</c> wird zurückgehalten.</summary>
    private static (string emit, string keep) SplitHoldback(string s)
    {
        var lastOpen = s.LastIndexOf('[');
        if (lastOpen >= 0 && s.IndexOf(']', lastOpen) < 0)
        {
            return (s[..lastOpen], s[lastOpen..]);
        }
        return (s, string.Empty);
    }

    private static (int index, string? payload) FindData(List<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var rest = line[5..];
                if (rest.StartsWith(' '))
                {
                    rest = rest[1..];
                }
                rest = rest.TrimEnd('\r');
                return (i, rest);
            }
        }
        return (-1, null);
    }

    private static string? GetString(JsonObject obj, string key)
        => obj.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null;

    private static int? GetInt(JsonObject obj, string key)
        => obj.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<int>(out var n) ? n : null;
}
