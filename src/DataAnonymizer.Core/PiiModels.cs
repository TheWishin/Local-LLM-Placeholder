namespace DataAnonymizer.Services;

/// <summary>Kategorien von personenbezogenen Daten, die erkannt werden.</summary>
public enum PiiCategory
{
    Begriff,      // Eigene Begriffe (Firmen, Projektnamen, ...)
    Email,
    Iban,
    Ahv,          // Schweizer AHV-Nummer (756.xxxx.xxxx.xx)
    Kreditkarte,
    Referenz,     // Kunden-/Policen-/Fall-/Dossier-Nummern
    Telefon,
    Datum,
    Adresse,      // Strasse + Hausnummer
    Ort,          // PLZ + Ortschaft
    Kennzeichen,  // Autokennzeichen (CH-Kantone)
    Name
}

/// <summary>Eine Zuordnung Platzhalter → Originalwert.</summary>
public sealed record MappingEntry(string Placeholder, string Original, PiiCategory Category);

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
    /// <summary>Geburtsdaten mit Kontext ("geb.", "geboren am", "Geburtsdatum:").</summary>
    public bool Geburtsdaten { get; set; } = true;
    /// <summary>Alle Datumsangaben ersetzen (kann den Fall-Zeitablauf unlesbar machen).</summary>
    public bool AlleDaten { get; set; } = false;

    public List<CustomTerm> EigeneBegriffe { get; set; } = new();
}
