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
    Organisation, // Firmen und Organisationen (wird nur vom lokalen LLM erkannt)
    Ip,           // IP-Adressen (IPv4/IPv6) – Personendaten nach DSG
    Sensitiv      // Besonders schützenswerte Personendaten nach revDSG (Gesundheit,
                  // Religion, politische Ansichten, Gewerkschaft, Biometrie, Strafverfahren …)
}

/// <summary>Eine Zuordnung Platzhalter → Originalwert.</summary>
public sealed record MappingEntry(string Placeholder, string Original, PiiCategory Category);

/// <summary>
/// Sprachneutrale IDs für den Export/Import von Zuordnungstabellen – identisch
/// mit den Kategorie-Schlüsseln der Browser-Erweiterung (extension/engine.js),
/// damit Dateien zwischen App und Erweiterung austauschbar sind.
/// </summary>
public static class PiiCategoryIds
{
    public static string ToId(PiiCategory category) => category switch
    {
        PiiCategory.Begriff => "term",
        PiiCategory.Email => "email",
        PiiCategory.Iban => "iban",
        PiiCategory.Ahv => "ssn",
        PiiCategory.Kreditkarte => "card",
        PiiCategory.Referenz => "ref",
        PiiCategory.Telefon => "phone",
        PiiCategory.Datum => "date",
        PiiCategory.Adresse => "address",
        PiiCategory.Ort => "city",
        PiiCategory.Kennzeichen => "plate",
        PiiCategory.Name => "name",
        PiiCategory.Organisation => "org",
        PiiCategory.Ip => "ip",
        PiiCategory.Sensitiv => "sensitive",
        _ => "term"
    };

    public static PiiCategory FromId(string? id) => id switch
    {
        "email" => PiiCategory.Email,
        "iban" => PiiCategory.Iban,
        "ssn" => PiiCategory.Ahv,
        "card" => PiiCategory.Kreditkarte,
        "ref" => PiiCategory.Referenz,
        "phone" => PiiCategory.Telefon,
        "date" => PiiCategory.Datum,
        "address" => PiiCategory.Adresse,
        "city" => PiiCategory.Ort,
        "plate" => PiiCategory.Kennzeichen,
        "name" => PiiCategory.Name,
        "org" => PiiCategory.Organisation,
        "ip" => PiiCategory.Ip,
        "sensitive" => PiiCategory.Sensitiv,
        _ => PiiCategory.Begriff
    };
}

/// <summary>Ein vom lokalen LLM gefundener Textabschnitt mit persönlichen Daten.</summary>
public sealed record LlmEntity(string Text, PiiCategory Category);

/// <summary>Ergebnis einer Anonymisierung.</summary>
public sealed class AnonymizationResult
{
    public string AnonymizedText { get; init; } = string.Empty;
    public IReadOnlyList<MappingEntry> Mappings { get; init; } = Array.Empty<MappingEntry>();
}

/// <summary>
/// Ergebnis der Anonymisierung mehrerer Texte mit gemeinsamer Zuordnungstabelle.
/// Die Reihenfolge von <see cref="AnonymizedTexts"/> entspricht der Eingabe.
/// </summary>
public sealed class MultiAnonymizationResult
{
    public IReadOnlyList<string> AnonymizedTexts { get; init; } = Array.Empty<string>();
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
    /// <summary>IP-Adressen (IPv4/IPv6).</summary>
    public bool IpAdressen { get; set; } = true;
    /// <summary>Besonders schützenswerte Daten nach revDSG (nur vom lokalen LLM gefunden).</summary>
    public bool SensitiveDaten { get; set; } = true;
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
