using System.Text;
using System.Text.RegularExpressions;

namespace DataAnonymizer.Services;

/// <summary>
/// Erkennt personenbezogene Daten in einem Text und ersetzt sie durch konsistente
/// Platzhalter wie [NAME_1] oder [EMAIL_2]. Gleicher Originalwert → gleicher Platzhalter.
/// Läuft vollständig lokal, es werden keine Daten übertragen.
/// </summary>
public sealed class AnonymizerService
{
    private sealed record Candidate(int Start, int Length, string Original, PiiCategory Category, int Priority);

    private static readonly Dictionary<PiiCategory, string> Labels = new()
    {
        [PiiCategory.Begriff] = "BEGRIFF",
        [PiiCategory.Email] = "EMAIL",
        [PiiCategory.Iban] = "IBAN",
        [PiiCategory.Ahv] = "AHV",
        [PiiCategory.Kreditkarte] = "KARTE",
        [PiiCategory.Referenz] = "REFERENZ",
        [PiiCategory.Telefon] = "TELEFON",
        [PiiCategory.Datum] = "DATUM",
        [PiiCategory.Adresse] = "ADRESSE",
        [PiiCategory.Ort] = "ORT",
        [PiiCategory.Kennzeichen] = "KENNZEICHEN",
        [PiiCategory.Name] = "NAME"
    };

    public static string LabelFor(PiiCategory category) => Labels[category];

    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    // ---- Erkennungsmuster -------------------------------------------------

    private static readonly Regex EmailRx = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", Opts);

    private static readonly Regex IbanRx = new(
        @"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]){11,30}\b", Opts);

    // Schweizer AHV-Nummer: 756.1234.5678.97
    private static readonly Regex AhvRx = new(
        @"\b756[.\s]?\d{4}[.\s]?\d{4}[.\s]?\d{2}\b", Opts);

    // Kandidaten für Kreditkarten (13–19 Ziffern), werden zusätzlich per Luhn geprüft.
    private static readonly Regex CardRx = new(
        @"(?<!\d)\d(?:[ \-]?\d){12,18}(?!\d)", Opts);

    private static readonly Regex PhoneRx = new(
        @"(?:\+|00)[1-9]\d{1,2}(?:[ \-/]?\(0\))?(?:[ \-/]?\d){6,12}" +   // +41 79 123 45 67
        @"|(?<![\d.])0\d{2}[ /\-]?\d{3}[ /\-]?\d{2}[ /\-]?\d{2}(?![\d.])" + // 044 123 45 67
        @"|(?<![\d.])0\d{3,4}[ /\-]\d{4,8}(?![\d.])",                     // 0621 1234567
        Opts);

    // 31.12.1980, 31.12.80, 1980-12-31
    private static readonly Regex DateRx = new(
        @"\b[0-3]?\d\.[01]?\d\.(?:\d{4}|\d{2})\b|\b(?:19|20)\d{2}-[01]\d-[0-3]\d\b", Opts);

    private static readonly Regex BirthDateRx = new(
        @"(?:geb\.|geboren\s+am|Geburtsdatum\s*:?)\s*(?<d>[0-3]?\d\.[01]?\d\.(?:\d{4}|\d{2})|(?:19|20)\d{2}-[01]\d-[0-3]\d)",
        Opts | RegexOptions.IgnoreCase);

    private const string Word = @"[A-ZÄÖÜ][A-Za-zÄÖÜäöüéèêàâçß\-]*";

    // Bahnhofstrasse 12a, Untere Mühleweg 3, Rue de Lausanne 12, Via Roma 8
    private static readonly Regex StreetRx = new(
        @"\b(?:" + Word + @"\s+)?" + Word +
        @"(?:strasse|straße|str\.|weg|gasse|platz|allee|ring|halde|rain|matte?|acker|feld|bühl|hof|damm|ufer|steig|promenade|quai|graben)" +
        @"\s+\d{1,4}\s?[a-hA-H]?\b" +
        @"|\b(?:Rue|Route|Avenue|Av\.|Chemin|Ch\.|Via|Viale|Piazza|Place|Boulevard|Bd\.)" +
        @"\s+(?:de\s+la\s+|des\s+|de\s+|du\s+|d')?" + Word + @"(?:\s+" + Word + @")?\s+\d{1,4}\b",
        Opts);

    // 8001 Zürich, 79576 Weil am Rhein
    private static readonly Regex PlzOrtRx = new(
        @"\b\d{4,5}\s+(?<ort>" + Word + @"(?:\s+(?:am|an\s+der|bei|ob|im)\s+" + Word + @"|\s+[A-Z]{2}\b)?)",
        Opts);

    // Herr Max Muster, Frau Dr. Anna Meier-Huber
    private static readonly Regex SalutationNameRx = new(
        @"\b(?:Herrn?|Frau|Hr\.|Fr\.)\s+(?:Dr\.\s*(?:med\.\s*)?|Prof\.\s*)?(?<name>" + Word + @"(?:\s+" + Word + @"){0,2})\b",
        Opts);

    // "Name: Max Muster", "Kunde: Anna Meier"
    private static readonly Regex KeywordNameRx = new(
        @"\b(?:Name|Vorname|Nachname|Kunde|Kundin|Patient(?:in)?|Versicherte[rn]?|Mieter(?:in)?|Mandant(?:in)?|Ansprechpartner(?:in)?|Kontakt)\s*:\s*(?<name>" +
        Word + @"(?:\s+" + Word + @"){0,3})\b",
        Opts);

    // ZH 123456, AG-345678
    private static readonly Regex PlateRx = new(
        @"\b(?:AG|AI|AR|BE|BL|BS|FR|GE|GL|GR|JU|LU|NE|NW|OW|SG|SH|SO|SZ|TG|TI|UR|VD|VS|ZG|ZH)[ \-]\d{3,6}\b", Opts);

    // Policen-Nr. 12345, Kundennummer: K-98765, Fall-Nr: 2023/0815
    private static readonly Regex ReferenceRx = new(
        @"\b(?:Policen?|Vertrags|Kunden|Fall|Dossier|Schaden|Rechnungs|Versicherten|Mitglieder)[\- ]?(?:Nr|Nummer|No)\.?\s*:?\s*(?<ref>[A-Za-z0-9][A-Za-z0-9.\-/]{2,})",
        Opts | RegexOptions.IgnoreCase);

    // Wörter, die nach "Herr/Frau <Name>" nicht Teil des Namens sind.
    private static readonly HashSet<string> NameStopWords = new(StringComparer.Ordinal)
    {
        "Am", "Im", "An", "Auf", "Aus", "Bei", "Der", "Die", "Das", "Den", "Dem",
        "Ein", "Eine", "Und", "Oder", "Vom", "Zum", "Zur", "Nach", "Mit", "Ist",
        "Hat", "Wird", "Kann", "Soll", "Sein", "Ihre", "Ihr", "Wir", "Sie", "Es"
    };

    private static readonly HashSet<string> MonthAndUnitWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August",
        "September", "Oktober", "November", "Dezember",
        "Uhr", "Franken", "Rappen", "Euro", "CHF", "EUR", "USD", "Prozent",
        "Personen", "Stück", "Jahre", "Jahren", "Mio", "Mrd", "Millionen",
        "Milliarden", "Kilometer", "Meter", "Minuten", "Stunden", "Tage",
        "Tagen", "Wochen", "Monate", "Monaten", "Zeichen", "Seiten"
    };

    // Wörter, die nach "Kontakt:" o.Ä. stehen können, aber keine Namen sind.
    private static readonly HashSet<string> NotANameWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tel", "Telefon", "Mobile", "Natel", "Fax", "Mail", "Email", "E-Mail", "Handy", "Siehe", "Unbekannt"
    };

    // ---- Öffentliche API --------------------------------------------------

    /// <summary>Anonymisiert den Text und liefert das Ergebnis inkl. Zuordnungstabelle.</summary>
    public AnonymizationResult Anonymize(string text, AnonymizerOptions options)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new AnonymizationResult();
        }

        var candidates = Collect(text, options);
        var accepted = ResolveOverlaps(candidates);

        // Platzhalter vergeben: gleicher Wert (pro Kategorie) → gleicher Platzhalter.
        var placeholderByValue = new Dictionary<(PiiCategory, string), string>();
        var counters = new Dictionary<PiiCategory, int>();
        var mappings = new List<MappingEntry>();

        var sb = new StringBuilder(text.Length);
        var pos = 0;
        foreach (var c in accepted.OrderBy(c => c.Start))
        {
            var key = (c.Category, Normalize(c.Original));
            if (!placeholderByValue.TryGetValue(key, out var placeholder))
            {
                var n = counters.GetValueOrDefault(c.Category) + 1;
                counters[c.Category] = n;
                placeholder = $"[{Labels[c.Category]}_{n}]";
                placeholderByValue[key] = placeholder;
                mappings.Add(new MappingEntry(placeholder, c.Original, c.Category));
            }

            sb.Append(text, pos, c.Start - pos);
            sb.Append(placeholder);
            pos = c.Start + c.Length;
        }
        sb.Append(text, pos, text.Length - pos);

        return new AnonymizationResult { AnonymizedText = sb.ToString(), Mappings = mappings };
    }

    /// <summary>Ersetzt Platzhalter in einem Text (z.B. einer KI-Antwort) wieder durch die Originalwerte.</summary>
    public string Deanonymize(string text, IEnumerable<MappingEntry> mappings)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var sb = new StringBuilder(text);
        foreach (var m in mappings)
        {
            sb.Replace(m.Placeholder, m.Original);
        }
        return sb.ToString();
    }

    // ---- Interne Logik ----------------------------------------------------

    private static string Normalize(string value) => Regex.Replace(value.Trim(), @"\s+", " ");

    private static List<Candidate> Collect(string text, AnonymizerOptions options)
    {
        var result = new List<Candidate>();
        var priority = 0;

        // Eigene Begriffe haben die höchste Priorität.
        foreach (var term in options.EigeneBegriffe)
        {
            if (string.IsNullOrWhiteSpace(term.Text))
            {
                continue;
            }
            var rx = new Regex(Regex.Escape(term.Text.Trim()), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            AddMatches(result, rx.Matches(text), PiiCategory.Begriff, priority);
        }

        priority++;
        if (options.Emails)
        {
            AddMatches(result, EmailRx.Matches(text), PiiCategory.Email, priority);
        }

        priority++;
        if (options.Iban)
        {
            AddMatches(result, IbanRx.Matches(text), PiiCategory.Iban, priority);
        }

        priority++;
        if (options.Ahv)
        {
            AddMatches(result, AhvRx.Matches(text), PiiCategory.Ahv, priority);
        }

        priority++;
        if (options.Kreditkarten)
        {
            foreach (Match m in CardRx.Matches(text))
            {
                if (IsLuhnValid(m.Value))
                {
                    result.Add(new Candidate(m.Index, m.Length, m.Value, PiiCategory.Kreditkarte, priority));
                }
            }
        }

        priority++;
        if (options.Referenzen)
        {
            foreach (Match m in ReferenceRx.Matches(text))
            {
                var g = m.Groups["ref"];
                if (!g.Success)
                {
                    continue;
                }
                // Satzzeichen am Ende gehören nicht zur Nummer ("Fall-Nr: 88123.").
                var value = g.Value.TrimEnd('.', '-', '/');
                if (value.Length >= 3)
                {
                    result.Add(new Candidate(g.Index, value.Length, value, PiiCategory.Referenz, priority));
                }
            }
        }

        priority++;
        if (options.Telefonnummern)
        {
            AddMatches(result, PhoneRx.Matches(text), PiiCategory.Telefon, priority);
        }

        priority++;
        if (options.AlleDaten)
        {
            AddMatches(result, DateRx.Matches(text), PiiCategory.Datum, priority);
        }
        else if (options.Geburtsdaten)
        {
            AddGroupMatches(result, BirthDateRx.Matches(text), "d", PiiCategory.Datum, priority);
        }

        priority++;
        if (options.Adressen)
        {
            AddMatches(result, StreetRx.Matches(text), PiiCategory.Adresse, priority);
        }

        priority++;
        if (options.Orte)
        {
            foreach (Match m in PlzOrtRx.Matches(text))
            {
                var ort = m.Groups["ort"].Value.Split(' ')[0];
                if (!MonthAndUnitWords.Contains(ort))
                {
                    result.Add(new Candidate(m.Index, m.Length, m.Value, PiiCategory.Ort, priority));
                }
            }
        }

        priority++;
        if (options.Kennzeichen)
        {
            foreach (Match m in PlateRx.Matches(text))
            {
                // "SG 2024" ist eher eine Jahreszahl als ein Kennzeichen.
                var digits = m.Value[3..].Trim();
                if (digits.Length == 4 && (digits.StartsWith("19") || digits.StartsWith("20")))
                {
                    continue;
                }
                result.Add(new Candidate(m.Index, m.Length, m.Value, PiiCategory.Kennzeichen, priority));
            }
        }

        priority++;
        if (options.Namen)
        {
            AddNameMatches(result, SalutationNameRx.Matches(text), priority);
            AddNameMatches(result, KeywordNameRx.Matches(text), priority);
        }

        return result;
    }

    private static void AddMatches(List<Candidate> result, MatchCollection matches, PiiCategory category, int priority)
    {
        foreach (Match m in matches)
        {
            result.Add(new Candidate(m.Index, m.Length, m.Value, category, priority));
        }
    }

    private static void AddGroupMatches(List<Candidate> result, MatchCollection matches, string groupName, PiiCategory category, int priority)
    {
        foreach (Match m in matches)
        {
            var g = m.Groups[groupName];
            if (g.Success)
            {
                result.Add(new Candidate(g.Index, g.Length, g.Value, category, priority));
            }
        }
    }

    private static void AddNameMatches(List<Candidate> result, MatchCollection matches, int priority)
    {
        foreach (Match m in matches)
        {
            var g = m.Groups["name"];
            if (!g.Success)
            {
                continue;
            }

            // Nachlaufende Wörter abschneiden, die keine Namen sind ("Herr Müller Am Montag ...").
            var words = g.Value.Split(' ');
            var keep = words.Length;
            while (keep > 1 && NameStopWords.Contains(words[keep - 1]))
            {
                keep--;
            }
            var value = string.Join(' ', words.Take(keep));
            if (keep == 1 && NotANameWords.Contains(value))
            {
                continue;
            }
            result.Add(new Candidate(g.Index, value.Length, value, PiiCategory.Name, priority));
        }
    }

    /// <summary>Bei Überschneidungen gewinnt die Regel mit der höheren Priorität, danach der längere Treffer.</summary>
    private static List<Candidate> ResolveOverlaps(List<Candidate> candidates)
    {
        var accepted = new List<Candidate>();
        foreach (var c in candidates.OrderBy(c => c.Priority).ThenByDescending(c => c.Length).ThenBy(c => c.Start))
        {
            var overlaps = accepted.Any(a => c.Start < a.Start + a.Length && a.Start < c.Start + c.Length);
            if (!overlaps)
            {
                accepted.Add(c);
            }
        }
        return accepted;
    }

    private static bool IsLuhnValid(string value)
    {
        var digits = value.Where(char.IsDigit).Select(ch => ch - '0').ToArray();
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i];
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }
            sum += d;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }
}
