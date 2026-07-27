using DataAnonymizer.Services;

namespace DataAnonymizer.Proxy;

/// <summary>Einstellungen des Anonymisierungs-Gateways.</summary>
public sealed class ProxyOptions
{
    /// <summary>Adresse des echten KI-Servers, an den weitergeleitet wird.</summary>
    public string Upstream { get; set; } = "https://api.anthropic.com";

    /// <summary>Sprache der erzeugten Platzhalter ([NAME_1] vs. [NOM_1]).</summary>
    public string Language { get; set; } = "En";

    /// <summary>
    /// Zusätzlich zum Muster-Erkenner auch das lokale LLM (Ollama) befragen.
    /// Findet mehr (Namen ohne Anrede, Firmen, sensible Angaben), kostet aber Zeit
    /// und setzt ein erreichbares Ollama voraus. Standardmässig aus.
    /// </summary>
    public bool UseLlm { get; set; } = false;

    /// <summary>Welche Kantone/Länder-übergreifende Kategorie-Schalter aktiv sind (alle an per Default).</summary>
    public AnonymizerOptions BuildAnonymizerOptions()
    {
        var options = new AnonymizerOptions
        {
            Language = ParseLanguage(Language)
        };
        return options;
    }

    public static AppLanguage ParseLanguage(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "de" or "german" or "deutsch" => AppLanguage.De,
        "fr" or "french" or "français" or "francais" => AppLanguage.Fr,
        "it" or "italian" or "italiano" => AppLanguage.It,
        _ => AppLanguage.En
    };
}
