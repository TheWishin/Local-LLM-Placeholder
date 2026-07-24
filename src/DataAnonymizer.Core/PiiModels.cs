namespace DataAnonymizer.Services;

/// <summary>Unterstützte Sprachen für Oberfläche und Platzhalter-Beschriftungen.</summary>
public enum AppLanguage
{
    De,
    En,
    Fr,
    It
}

/// <summary>Kategorien von personenbezogenen Daten, die erkannt werden.</summary>
public enum PiiCategory
{
    Begriff,      // Eigene Begriffe (Firmen, Projektnamen, ...)
    Email,
    Iban,
    Ahv,          // Sozialversicherungsnummern (CH AHV 756.xxxx.xxxx.xx, US SSN)
    Kreditkarte,
    Referenz,     // Kunden-/Policen-/Fall-/Dossier-Nummern
    Telefon,
    Datum,
    Adresse,      // Strasse + Hausnummer
    Ort,          // PLZ + Ortschaft
    Kennzeichen,  // Autokennzeichen (CH-Kantone)
    Name,
    Organisation  // Firmen und Organisationen (wird nur vom lokalen LLM erkannt)
}

/// <summary>Eine Zuordnung Platzhalter → Originalwert.</summary>
public sealed record MappingEntry(string Placeholder, string Original, PiiCategory Category);

/// <summary>Ein vom lokalen LLM gefundener Textabschnitt mit persönlichen Daten.</summary>
public sealed record LlmEntity(string Text, PiiCategory Category);

/// <summary>Ergebnis einer Anonymisierung.</summary>
public sealed class AnonymizationResult
{
    public string AnonymizedText { get; init; } = string.Empty;
    public IReadOnlyList<MappingEntry> Mappings { get; init; } = Array.Empty<MappingEntry>();
}

/// <summary>Ein vom Benutzer definierter Begriff, der immer ersetzt wird.</summary>
public sealed record CustomTerm(string Text);

/// <summary>Welche Erkennungsregeln aktiv sind.</summary>
public sealed class AnonymizerOptions
{
    public bool Namen { get; set; } = true;
    public bool Emails { get; set; } = true;
    public bool Telefonnummern { get; set; } = true;
    public bool Adressen { get; set; } = true;
    public bool Orte { get; set; } = true;
    public bool Iban { get; set; } = true;
    public bool Ahv { get; set; } = true;
    public bool Kreditkarten { get; set; } = true;
    public bool Referenzen { get; set; } = true;
    public bool Kennzeichen { get; set; } = true;
    /// <summary>Firmen/Organisationen (werden nur vom lokalen LLM gefunden).</summary>
    public bool Organisationen { get; set; } = true;
    /// <summary>Geburtsdaten mit Kontext ("geb.", "born on", "né le", "nato il").</summary>
    public bool Geburtsdaten { get; set; } = true;
    /// <summary>Alle Datumsangaben ersetzen (kann den Fall-Zeitablauf unlesbar machen).</summary>
    public bool AlleDaten { get; set; } = false;

    /// <summary>Sprache der erzeugten Platzhalter, z.B. [TELEFON_1] (De) vs. [PHONE_1] (En).</summary>
    public AppLanguage Language { get; set; } = AppLanguage.De;

    public List<CustomTerm> EigeneBegriffe { get; set; } = new();

    /// <summary>
    /// Werte, die nie ersetzt werden, obwohl die Erkennung sie findet –
    /// z.B. per Klick auf einen Eintrag der Zuordnungstabelle freigegeben.
    /// Vergleich: ohne Gross-/Kleinschreibung, Leerraum normalisiert.
    /// </summary>
    public List<string> ErlaubteWerte { get; set; } = new();
}
