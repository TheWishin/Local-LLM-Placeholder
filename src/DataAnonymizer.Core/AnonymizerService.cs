using System.Text;
using System.Text.RegularExpressions;

namespace DataAnonymizer.Services;

/// <summary>
/// Erkennt personenbezogene Daten in einem Text und ersetzt sie durch konsistente
/// Platzhalter wie [NAME_1] oder [EMAIL_2]. Gleicher Originalwert → gleicher Platzhalter.
/// Die Muster decken Deutsch, Englisch, Französisch und Italienisch ab; zusätzlich
/// können Funde eines lokalen LLM übergeben werden. Läuft vollständig lokal.
/// </summary>
public sealed class AnonymizerService
{
    private sealed record Candidate(int Start, int Length, string Original, PiiCategory Category, int Priority);

    private static readonly Dictionary<AppLanguage, Dictionary<PiiCategory, string>> LabelsByLanguage = new()
    {
        [AppLanguage.De] = new()
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
            [PiiCategory.Name] = "NAME",
            [PiiCategory.Organisation] = "FIRMA"
        },
        [AppLanguage.En] = new()
        {
            [PiiCategory.Begriff] = "TERM",
            [PiiCategory.Email] = "EMAIL",
            [PiiCategory.Iban] = "IBAN",
            [PiiCategory.Ahv] = "SSN",
            [PiiCategory.Kreditkarte] = "CARD",
            [PiiCategory.Referenz] = "REFERENCE",
            [PiiCategory.Telefon] = "PHONE",
            [PiiCategory.Datum] = "DATE",
            [PiiCategory.Adresse] = "ADDRESS",
            [PiiCategory.Ort] = "CITY",
            [PiiCategory.Kennzeichen] = "PLATE",
            [PiiCategory.Name] = "NAME",
            [PiiCategory.Organisation] = "COMPANY"
        },
        [AppLanguage.Fr] = new()
        {
            [PiiCategory.Begriff] = "TERME",
            [PiiCategory.Email] = "EMAIL",
            [PiiCategory.Iban] = "IBAN",
            [PiiCategory.Ahv] = "AVS",
            [PiiCategory.Kreditkarte] = "CARTE",
            [PiiCategory.Referenz] = "REFERENCE",
            [PiiCategory.Telefon] = "TELEPHONE",
            [PiiCategory.Datum] = "DATE",
            [PiiCategory.Adresse] = "ADRESSE",
            [PiiCategory.Ort] = "LIEU",
            [PiiCategory.Kennzeichen] = "PLAQUE",
            [PiiCategory.Name] = "NOM",
            [PiiCategory.Organisation] = "SOCIETE"
        },
        [AppLanguage.It] = new()
        {
            [PiiCategory.Begriff] = "TERMINE",
            [PiiCategory.Email] = "EMAIL",
            [PiiCategory.Iban] = "IBAN",
            [PiiCategory.Ahv] = "AVS",
            [PiiCategory.Kreditkarte] = "CARTA",
            [PiiCategory.Referenz] = "RIFERIMENTO",
            [PiiCategory.Telefon] = "TELEFONO",
            [PiiCategory.Datum] = "DATA",
            [PiiCategory.Adresse] = "INDIRIZZO",
            [PiiCategory.Ort] = "LUOGO",
            [PiiCategory.Kennzeichen] = "TARGA",
            [PiiCategory.Name] = "NOME",
            [PiiCategory.Organisation] = "DITTA"
        }
    };

    public static string LabelFor(PiiCategory category) => LabelFor(category, AppLanguage.De);

    public static string LabelFor(PiiCategory category, AppLanguage language) => LabelsByLanguage[language][category];

    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    // ---- Erkennungsmuster (DE/EN/FR/IT) -----------------------------------

    private static readonly Regex EmailRx = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", Opts);

    private static readonly Regex IbanRx = new(
        @"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]){11,30}\b", Opts);

    // Schweizer AHV-Nummer (756.1234.5678.97) und US Social Security Number (123-45-6789)
    private static readonly Regex SocialSecurityRx = new(
        @"\b756[.\s]?\d{4}[.\s]?\d{4}[.\s]?\d{2}\b|\b\d{3}-\d{2}-\d{4}\b", Opts);

    // Kandidaten für Kreditkarten (13–19 Ziffern), werden zusätzlich per Luhn geprüft.
    private static readonly Regex CardRx = new(
        @"(?<!\d)\d(?:[ \-]?\d){12,18}(?!\d)", Opts);

    private static readonly Regex PhoneRx = new(
        @"(?:\+|00)[1-9]\d{1,2}(?:[ \-/]?\(0\))?(?:[ \-/]?\d){6,12}" +   // +41 79 123 45 67
        @"|(?<![\d.])0\d{2}[ /\-]?\d{3}[ /\-]?\d{2}[ /\-]?\d{2}(?![\d.])" + // 044 123 45 67
        @"|(?<![\d.])0\d{3,4}[ /\-]\d{4,8}(?![\d.])" +                    // 0621 1234567
        @"|\(\d{3}\)\s?\d{3}[-. ]\d{4}\b" +                               // (555) 123-4567
        @"|(?<![\d.\-])\d{3}[-.]\d{3}[-.]\d{4}(?![\d.\-])",               // 555-123-4567
        Opts);

    // Numerische Daten: 31.12.1980, 31.12.80, 1980-12-31, 12/31/1980, 31/12/80
    private const string NumericDate =
        @"[0-3]?\d\.[01]?\d\.(?:\d{4}|\d{2})|(?:19|20)\d{2}-[01]\d-[0-3]\d|\d{1,2}/\d{1,2}/(?:\d{4}|\d{2})";

    private static readonly Regex DateRx = new(
        @"\b(?:" + NumericDate + @")\b", Opts);

    // Monatsnamen DE/EN/FR/IT für ausgeschriebene Geburtsdaten.
    private const string MonthName =
        @"(?:Januar|Februar|März|April|Mai|Juni|Juli|August|September|Oktober|November|Dezember" +
        @"|January|February|March|May|June|July|October|December" +
        @"|janvier|février|mars|avril|juin|juillet|août|septembre|octobre|novembre|décembre" +
        @"|gennaio|febbraio|marzo|aprile|maggio|giugno|luglio|agosto|settembre|ottobre|dicembre)";

    // 12. März 1985 / 12 mars 1985 / March 12, 1985
    private const string WrittenDate =
        @"\d{1,2}\.?\s+" + MonthName + @"\s+\d{4}|" + MonthName + @"\s+\d{1,2},?\s+\d{4}";

    private static readonly Regex BirthDateRx = new(
        @"(?:geb\.|geboren\s+am|Geburtsdatum\s*:?" +
        @"|born\s+on|born\s*:?|date\s+of\s+birth\s*:?|DOB\s*:?" +
        @"|n[ée]e?\s+le|date\s+de\s+naissance\s*:?" +
        @"|nat[oa]\s+il|data\s+di\s+nascita\s*:?)\s*" +
        @"(?<d>" + NumericDate + @"|" + WrittenDate + @")",
        Opts | RegexOptions.IgnoreCase);

    private const string Word = @"[A-ZÄÖÜ][A-Za-zÄÖÜäöüéèêàâçß\-]*";

    // Bahnhofstrasse 12a, Rue de Lausanne 12, Via Roma 8, 12 Main Street
    private static readonly Regex StreetRx = new(
        @"\b(?:" + Word + @"\s+)?" + Word +
        @"(?:strasse|straße|str\.|weg|gasse|platz|allee|ring|halde|rain|matte?|acker|feld|bühl|hof|damm|ufer|steig|promenade|quai|graben)" +
        @"\s+\d{1,4}\s?[a-hA-H]?\b" +
        @"|\b(?:Rue|Route|Avenue|Av\.|Chemin|Ch\.|Via|Viale|Piazza|Place|Boulevard|Bd\.)" +
        @"\s+(?:de\s+la\s+|des\s+|de\s+|du\s+|d')?" + Word + @"(?:\s+" + Word + @")?\s+\d{1,4}\b" +
        @"|\b\d{1,5}\s+" + Word + @"(?:\s+" + Word + @")?\s+" +
        @"(?:Street|St\.|Road|Rd\.?|Avenue|Ave\.?|Lane|Ln\.?|Drive|Boulevard|Blvd\.?|Court|Ct\.?|Way|Square|Terrace|Crescent|Close)(?=[\s,.;]|$)",
        Opts);

    // 8001 Zürich, 79576 Weil am Rhein, 1201 Genève
    private static readonly Regex PlzOrtRx = new(
        @"\b\d{4,5}\s+(?<ort>" + Word + @"(?:\s+(?:am|an\s+der|bei|ob|im)\s+" + Word + @"|\s+[A-Z]{2}\b)?)",
        Opts);

    // Herr Max Muster, Mr. John Smith, Madame Claire Dubois, Sig. Mario Rossi
    private static readonly Regex SalutationNameRx = new(
        @"\b(?:Herrn?|Frau|Hr\.|Fr\." +
        @"|Mrs\.?|Mr\.?|Ms\.?|Miss" +
        @"|Monsieur|Madame|Mademoiselle|Mme|Mlle|M\." +
        @"|Signora|Signorina|Signor|Sig\.ra|Sig\.|Dott\.ssa|Dott\." +
        @"|Dr\.|Prof\.)" +
        @"\s+(?:Dr\.\s*(?:med\.\s*)?|Prof\.\s*)?(?<name>" + Word + @"(?:\s+" + Word + @"){0,2})\b",
        Opts);

    // "Name: Max Muster", "Customer: John Smith", "Client : Jean Dupont", "Cliente: Mario Rossi"
    private static readonly Regex KeywordNameRx = new(
        @"\b(?:Name|Vorname|Nachname|Kunde|Kundin|Patient(?:in)?|Versicherte[rn]?|Mieter(?:in)?|Mandant(?:in)?|Ansprechpartner(?:in)?|Kontakt" +
        @"|Customer|Client(?:e)?|Insured|Tenant|Policyholder|Surname|First\s+name|Last\s+name" +
        @"|Assur[ée]e?|Locataire|Pr[ée]nom|Nom" +
        @"|Paziente|Assicurat[oa]|Inquilin[oa]|Cognome|Nome|Contatto)" +
        @"\s*:\s*(?<name>" + Word + @"(?:\s+" + Word + @"){0,3})\b",
        Opts);

    // ZH 123456, AG-345678
    private static readonly Regex PlateRx = new(
        @"\b(?:AG|AI|AR|BE|BL|BS|FR|GE|GL|GR|JU|LU|NE|NW|OW|SG|SH|SO|SZ|TG|TI|UR|VD|VS|ZG|ZH)[ \-]\d{3,6}\b", Opts);

    // Policen-Nr. 12345, Policy No. P-123, N° de police 4711, Numero polizza 88/99
    private static readonly Regex ReferenceRx = new(
        @"\b(?:(?:Policen?|Vertrags|Kunden|Fall|Dossier|Schaden|Rechnungs|Versicherten|Mitglieder)[\- ]?(?:Nr|Nummer|No)\.?" +
        @"|(?:Policy|Contract|Customer|Case|Claim|Invoice|Member|File|Reference)[\- ]?(?:No|Number|Nr|#)\.?" +
        @"|(?:N[°o]|Num[ée]ro)\.?\s*(?:de\s+)?(?:police|contrat|client|dossier|sinistre|facture|r[ée]f[ée]rence)" +
        @"|(?:Police|Contrat|Dossier|Sinistre|Facture|R[ée]f[ée]rence)\s*(?:n[°o]|num[ée]ro)\.?" +
        @"|(?:Numero|N\.)\s*(?:di\s+)?(?:polizza|contratto|cliente|pratica|sinistro|fattura|riferimento)" +
        @"|(?:Polizza|Contratto|Pratica|Sinistro|Fattura|Riferimento)\s*(?:n[°o.]|numero)\.?)" +
        @"\s*:?\s*(?<ref>[A-Za-z0-9][A-Za-z0-9.\-/]{2,})",
        Opts | RegexOptions.IgnoreCase);

    // Wörter, die nach einer Anrede nicht Teil des Namens sind.
    private static readonly HashSet<string> NameStopWords = new(StringComparer.Ordinal)
    {
        "Am", "Im", "An", "Auf", "Aus", "Bei", "Der", "Die", "Das", "Den", "Dem",
        "Ein", "Eine", "Und", "Oder", "Vom", "Zum", "Zur", "Nach", "Mit", "Ist",
        "Hat", "Wird", "Kann", "Soll", "Sein", "Ihre", "Ihr", "Wir", "Sie", "Es",
        "The", "A", "An", "And", "Or", "Is", "Was", "Has", "Had", "Will", "Can", "On", "At", "In",
        "Le", "La", "Les", "Et", "Ou", "Il", "Elle", "Est", "Un", "Une", "Ce", "Cette",
        "Lo", "Gli", "E", "O", "È", "Ha", "Sono", "Questo", "Questa"
    };

    private static readonly HashSet<string> MonthAndUnitWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August",
        "September", "Oktober", "November", "Dezember",
        "January", "February", "March", "May", "June", "July", "October", "December",
        "Janvier", "Février", "Mars", "Avril", "Juin", "Juillet", "Août",
        "Septembre", "Octobre", "Novembre", "Décembre",
        "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno", "Luglio",
        "Agosto", "Settembre", "Ottobre", "Dicembre",
        "Uhr", "Franken", "Rappen", "Euro", "CHF", "EUR", "USD", "Prozent",
        "Personen", "Stück", "Jahre", "Jahren", "Mio", "Mrd", "Millionen",
        "Milliarden", "Kilometer", "Meter", "Minuten", "Stunden", "Tage",
        "Tagen", "Wochen", "Monate", "Monaten", "Zeichen", "Seiten",
        "Dollars", "Pounds", "Percent", "People", "Years", "Hours", "Minutes",
        "Days", "Weeks", "Months", "Miles", "Pages",
        "Francs", "Personnes", "Ans", "Heures", "Pour",
        "Franchi", "Persone", "Anni", "Ore", "Giorni"
    };

    // Wörter, die nach "Kontakt:" o.Ä. stehen können, aber keine Namen sind.
    private static readonly HashSet<string> NotANameWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tel", "Telefon", "Mobile", "Natel", "Fax", "Mail", "Email", "E-Mail", "Handy", "Siehe", "Unbekannt",
        "Phone", "See", "Unknown", "None", "Voir", "Aucun", "Inconnu", "Vedi", "Nessuno", "Sconosciuto"
    };

    // ---- Öffentliche API --------------------------------------------------

    /// <summary>
    /// Anonymisiert den Text und liefert das Ergebnis inkl. Zuordnungstabelle.
    /// Optional können Funde eines lokalen LLM übergeben werden; bei Überschneidungen
    /// haben die präziseren Regex-Treffer Vorrang.
    /// </summary>
    public AnonymizationResult Anonymize(string text, AnonymizerOptions options, IReadOnlyCollection<LlmEntity>? llmFindings = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new AnonymizationResult();
        }

        var candidates = Collect(text, options, llmFindings);

        // Vom Benutzer freigegebene Werte vor der Überlappungs-Auflösung entfernen,
        // damit sie auch keine anderen Treffer verdrängen.
        if (options.ErlaubteWerte.Count > 0)
        {
            var allowed = new HashSet<string>(
                options.ErlaubteWerte.Where(w => !string.IsNullOrWhiteSpace(w)).Select(Normalize),
                StringComparer.OrdinalIgnoreCase);
            candidates.RemoveAll(c => allowed.Contains(Normalize(c.Original)));
        }

        var accepted = ResolveOverlaps(candidates);

        // Platzhalter vergeben: gleicher Wert (pro Kategorie) → gleicher Platzhalter.
        var placeholderByValue = new Dictionary<(PiiCategory, string), string>();
        var counters = new Dictionary<PiiCategory, int>();
        var mappings = new List<MappingEntry>();
        var labels = LabelsByLanguage[options.Language];

        var sb = new StringBuilder(text.Length);
        var pos = 0;
        foreach (var c in accepted.OrderBy(c => c.Start))
        {
            var key = (c.Category, Normalize(c.Original));
            if (!placeholderByValue.TryGetValue(key, out var placeholder))
            {
                var n = counters.GetValueOrDefault(c.Category) + 1;
                counters[c.Category] = n;
                placeholder = $"[{labels[c.Category]}_{n}]";
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

    private static List<Candidate> Collect(string text, AnonymizerOptions options, IReadOnlyCollection<LlmEntity>? llmFindings)
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
            AddMatches(result, SocialSecurityRx.Matches(text), PiiCategory.Ahv, priority);
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
        if (options.Geburtsdaten || options.AlleDaten)
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

        // LLM-Funde zuletzt: Regex-Treffer sind bei Überschneidungen präziser.
        priority++;
        if (llmFindings is { Count: > 0 })
        {
            AddLlmCandidates(result, text, llmFindings, options, priority);
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
            if (keep == 1 && (NameStopWords.Contains(value) || NotANameWords.Contains(value)))
            {
                continue;
            }
            result.Add(new Candidate(g.Index, value.Length, value, PiiCategory.Name, priority));
        }
    }

    private static void AddLlmCandidates(List<Candidate> result, string text, IReadOnlyCollection<LlmEntity> findings, AnonymizerOptions options, int priority)
    {
        foreach (var finding in findings)
        {
            var value = finding.Text?.Trim();
            if (string.IsNullOrEmpty(value) || value.Length < 2 || value.Contains('[') || value.Contains(']'))
            {
                continue;
            }
            if (!IsCategoryEnabled(finding.Category, options))
            {
                continue;
            }

            var found = AddOccurrences(result, text, value, finding.Category, priority, StringComparison.Ordinal);
            if (!found)
            {
                // Das LLM gibt den Text nicht immer buchstabengetreu zurück – zweiter Versuch ohne Gross-/Kleinschreibung.
                AddOccurrences(result, text, value, finding.Category, priority, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static bool AddOccurrences(List<Candidate> result, string text, string value, PiiCategory category, int priority, StringComparison comparison)
    {
        var found = false;
        var idx = 0;
        while (idx <= text.Length - value.Length && (idx = text.IndexOf(value, idx, comparison)) >= 0)
        {
            var original = text.Substring(idx, value.Length);
            result.Add(new Candidate(idx, value.Length, original, category, priority));
            found = true;
            idx += value.Length;
        }
        return found;
    }

    private static bool IsCategoryEnabled(PiiCategory category, AnonymizerOptions options) => category switch
    {
        PiiCategory.Name => options.Namen,
        PiiCategory.Email => options.Emails,
        PiiCategory.Telefon => options.Telefonnummern,
        PiiCategory.Adresse => options.Adressen,
        PiiCategory.Ort => options.Orte,
        PiiCategory.Iban => options.Iban,
        PiiCategory.Ahv => options.Ahv,
        PiiCategory.Kreditkarte => options.Kreditkarten,
        PiiCategory.Referenz => options.Referenzen,
        PiiCategory.Kennzeichen => options.Kennzeichen,
        PiiCategory.Organisation => options.Organisationen,
        PiiCategory.Datum => options.Geburtsdaten || options.AlleDaten,
        _ => true
    };

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
