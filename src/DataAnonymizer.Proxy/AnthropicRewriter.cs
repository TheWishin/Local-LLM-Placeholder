using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DataAnonymizer.Services;

namespace DataAnonymizer.Proxy;

/// <summary>Ergebnis der Anonymisierung eines Anfrage-Bodys.</summary>
public sealed class RewriteResult
{
    public string Json { get; init; } = string.Empty;
    public IReadOnlyList<MappingEntry> Mappings { get; init; } = Array.Empty<MappingEntry>();
}

/// <summary>
/// Reine Logik des Anonymisierungs-Gateways: schreibt einen Anthropic-kompatiblen
/// Anfrage-Body so um, dass alle vertraulichen Freitexte durch Platzhalter ersetzt
/// sind, und ersetzt die Platzhalter in der Antwort wieder durch die Originalwerte.
/// Alles läuft ohne Netzwerk und ist damit vollständig testbar.
///
/// Weg der Daten:  App  →  [Gateway: anonymisieren]  →  Claude-Server
///                 App  ←  [Gateway: rückübersetzen]  ←  Claude-Server
/// </summary>
public static class AnthropicRewriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        // Zeichen wie < > & nicht escapen – die Anthropic-API erwartet reines UTF-8.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ---- Anfrage: vertrauliche Texte → Platzhalter -----------------------

    /// <summary>
    /// Ersetzt in einem Anthropic-Messages-Body (<c>/v1/messages</c>) alle Freitexte
    /// (System-Prompt, Nachrichten-Inhalte, Tool-Ergebnisse) durch Platzhalter.
    /// Über die gesamte Anfrage hinweg wird eine gemeinsame Zuordnungstabelle benutzt,
    /// damit derselbe Wert überall denselben Platzhalter erhält.
    /// </summary>
    public static RewriteResult AnonymizeRequestBody(
        string json,
        AnonymizerService service,
        AnonymizerOptions options,
        IReadOnlyCollection<LlmEntity>? llmFindings = null)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            // Kein gültiges JSON – unverändert weiterreichen, nichts zu anonymisieren.
            return new RewriteResult { Json = json };
        }
        if (root is not JsonObject obj)
        {
            return new RewriteResult { Json = json };
        }

        var slots = new List<TextSlot>();
        CollectRequestSlots(obj, slots);

        if (slots.Count == 0)
        {
            return new RewriteResult { Json = json };
        }

        var texts = slots.Select(s => s.Text).ToList();
        var result = service.AnonymizeMany(texts, options, llmFindings);
        for (var i = 0; i < slots.Count; i++)
        {
            slots[i].Set(result.AnonymizedTexts[i]);
        }

        return new RewriteResult
        {
            Json = obj.ToJsonString(SerializerOptions),
            Mappings = result.Mappings
        };
    }

    /// <summary>
    /// Liefert die reinen Freitexte einer Anfrage (ohne sie zu verändern) – z.B. um
    /// vor dem Anonymisieren das lokale LLM darüber laufen zu lassen.
    /// </summary>
    public static IReadOnlyList<string> ExtractRequestTexts(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
        if (root is not JsonObject obj)
        {
            return Array.Empty<string>();
        }
        var slots = new List<TextSlot>();
        CollectRequestSlots(obj, slots);
        return slots.Select(s => s.Text).ToList();
    }

    /// <summary>Ein Freitextfeld im Body samt Setter, um es zu ersetzen.</summary>
    private sealed record TextSlot(string Text, Action<string> Set);

    private static void CollectRequestSlots(JsonObject root, List<TextSlot> slots)
    {
        // System-Prompt: entweder ein String oder eine Liste von Text-Blöcken.
        if (root.TryGetPropertyValue("system", out var system))
        {
            if (system is JsonValue sv && sv.TryGetValue<string>(out var s))
            {
                slots.Add(new TextSlot(s, v => root["system"] = v));
            }
            else if (system is JsonArray sarr)
            {
                CollectTextBlocks(sarr, slots);
            }
        }

        if (root.TryGetPropertyValue("messages", out var messages) && messages is JsonArray messageArray)
        {
            foreach (var message in messageArray)
            {
                if (message is not JsonObject mo || !mo.TryGetPropertyValue("content", out var content))
                {
                    continue;
                }
                if (content is JsonValue cv && cv.TryGetValue<string>(out var c))
                {
                    slots.Add(new TextSlot(c, v => mo["content"] = v));
                }
                else if (content is JsonArray carr)
                {
                    CollectTextBlocks(carr, slots);
                }
            }
        }
    }

    /// <summary>
    /// Sammelt Text aus einer Block-Liste: <c>{"type":"text","text":...}</c> und
    /// <c>tool_result</c>-Blöcke (deren Inhalt wieder String oder Block-Liste ist).
    /// </summary>
    private static void CollectTextBlocks(JsonArray blocks, List<TextSlot> slots)
    {
        foreach (var block in blocks)
        {
            if (block is not JsonObject bo)
            {
                continue;
            }
            var type = (bo.TryGetPropertyValue("type", out var t) && t is JsonValue tv && tv.TryGetValue<string>(out var ts)) ? ts : null;

            if (type == "text" && bo.TryGetPropertyValue("text", out var text) && text is JsonValue txv && txv.TryGetValue<string>(out var txt))
            {
                slots.Add(new TextSlot(txt, v => bo["text"] = v));
            }
            else if (type == "tool_result" && bo.TryGetPropertyValue("content", out var trContent))
            {
                if (trContent is JsonValue trv && trv.TryGetValue<string>(out var trs))
                {
                    slots.Add(new TextSlot(trs, v => bo["content"] = v));
                }
                else if (trContent is JsonArray trArr)
                {
                    CollectTextBlocks(trArr, slots);
                }
            }
        }
    }

    // ---- Antwort (nicht gestreamt): Platzhalter → Originalwerte -----------

    /// <summary>
    /// Ersetzt in einer vollständigen (nicht gestreamten) JSON-Antwort alle Platzhalter
    /// wieder durch die Originalwerte. Es werden alle String-Werte durchsucht – dadurch
    /// kommen auch Platzhalter in Tool-Argumenten (z.B. ein von der KI erzeugtes
    /// SQL-Skript) korrekt als echte Werte zurück. Nicht-Platzhalter bleiben unberührt.
    /// </summary>
    public static string DeanonymizeResponseBody(string json, IReadOnlyList<MappingEntry> mappings, AnonymizerService service)
    {
        if (mappings.Count == 0)
        {
            return json;
        }

        // Die Rückübersetzungs-Funktion EINMAL bauen und auf alle Strings anwenden.
        var deanon = service.BuildDeanonymizer(mappings);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            // Kein JSON (z.B. eine Fehlermeldung im Klartext): direkt ersetzen.
            return deanon(json);
        }
        if (root is null)
        {
            return json;
        }

        DeanonymizeNode(root, deanon);
        return root.ToJsonString(SerializerOptions);
    }

    private static void DeanonymizeNode(JsonNode node, Func<string, string> deanon)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    var child = obj[key];
                    if (child is JsonValue val && val.TryGetValue<string>(out var s))
                    {
                        var restored = deanon(s);
                        if (!ReferenceEquals(restored, s) && restored != s)
                        {
                            obj[key] = restored;
                        }
                    }
                    else if (child is not null)
                    {
                        DeanonymizeNode(child, deanon);
                    }
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    var child = arr[i];
                    if (child is JsonValue val && val.TryGetValue<string>(out var s))
                    {
                        var restored = deanon(s);
                        if (restored != s)
                        {
                            arr[i] = restored;
                        }
                    }
                    else if (child is not null)
                    {
                        DeanonymizeNode(child, deanon);
                    }
                }
                break;
        }
    }

    // ---- JSON-escaping für Streaming-Fragmente ---------------------------

    /// <summary>Escaped einen String für die Verwendung innerhalb eines JSON-Strings (ohne Anführungszeichen).</summary>
    public static string JsonEscapeInner(string value)
    {
        var encoded = JsonSerializer.Serialize(value, SerializerOptions);
        return encoded.Length >= 2 ? encoded[1..^1] : encoded;
    }
}
