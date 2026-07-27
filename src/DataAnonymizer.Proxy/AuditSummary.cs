using DataAnonymizer.Services;

namespace DataAnonymizer.Proxy;

/// <summary>
/// Erzeugt eine datenschutzfreundliche Kurzfassung einer Anonymisierung für das
/// Protokoll (revDSG-Rechenschaftspflicht): nur wie viele Werte je Kategorie
/// ersetzt wurden – niemals die Originalwerte selbst. So lässt sich belegen,
/// dass das Gateway arbeitet, ohne dabei neue personenbezogene Daten zu erzeugen.
/// </summary>
public static class AuditSummary
{
    /// <summary>z.B. "3 Platzhalter (name×1, email×1, iban×1)" oder "keine PII".</summary>
    public static string Summarize(IReadOnlyList<MappingEntry> mappings)
    {
        if (mappings is null || mappings.Count == 0)
        {
            return "keine PII";
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var m in mappings)
        {
            var id = PiiCategoryIds.ToId(m.Category);
            counts[id] = counts.GetValueOrDefault(id) + 1;
        }

        var parts = counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}×{kv.Value}");

        return $"{mappings.Count} Platzhalter ({string.Join(", ", parts)})";
    }
}
