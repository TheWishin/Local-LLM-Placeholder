// Erkennungs- und Ersetzungslogik – JavaScript-Portierung von
// src/DataAnonymizer.Core/AnonymizerService.cs. Bei Änderungen beide Seiten
// synchron halten. Läuft vollständig lokal im Browser.

export const CATEGORIES = ['term', 'email', 'iban', 'ssn', 'card', 'ref', 'phone', 'date', 'address', 'city', 'plate', 'name', 'org', 'ip', 'sensitive'];

const LABELS = {
    de: { term: 'BEGRIFF', email: 'EMAIL', iban: 'IBAN', ssn: 'AHV', card: 'KARTE', ref: 'REFERENZ', phone: 'TELEFON', date: 'DATUM', address: 'ADRESSE', city: 'ORT', plate: 'KENNZEICHEN', name: 'NAME', org: 'FIRMA', ip: 'IP', sensitive: 'SENSIBEL' },
    en: { term: 'TERM', email: 'EMAIL', iban: 'IBAN', ssn: 'SSN', card: 'CARD', ref: 'REFERENCE', phone: 'PHONE', date: 'DATE', address: 'ADDRESS', city: 'CITY', plate: 'PLATE', name: 'NAME', org: 'COMPANY', ip: 'IP', sensitive: 'SENSITIVE' },
    fr: { term: 'TERME', email: 'EMAIL', iban: 'IBAN', ssn: 'AVS', card: 'CARTE', ref: 'REFERENCE', phone: 'TELEPHONE', date: 'DATE', address: 'ADRESSE', city: 'LIEU', plate: 'PLAQUE', name: 'NOM', org: 'SOCIETE', ip: 'IP', sensitive: 'SENSIBLE' },
    it: { term: 'TERMINE', email: 'EMAIL', iban: 'IBAN', ssn: 'AVS', card: 'CARTA', ref: 'RIFERIMENTO', phone: 'TELEFONO', date: 'DATA', address: 'INDIRIZZO', city: 'LUOGO', plate: 'TARGA', name: 'NOME', org: 'DITTA', ip: 'IP', sensitive: 'SENSIBILE' }
};

export function labelFor(category, language) {
    return (LABELS[language] ?? LABELS.de)[category];
}

export function defaultOptions() {
    return {
        language: 'de',
        names: true, emails: true, phones: true, streets: true, cities: true,
        iban: true, ssn: true, cards: true, refs: true, plates: true, orgs: true,
        ip: true, sensitive: true,
        birthdays: true, allDates: false,
        customTerms: [],
        // Werte, die nie ersetzt werden, obwohl die Erkennung sie findet
        // (z.B. per Klick auf 👁 in der Zuordnungstabelle freigegeben).
        allowedTerms: []
    };
}

// ---- Erkennungsmuster (DE/EN/FR/IT) ---------------------------------------

const WORD = '[A-ZÄÖÜ][A-Za-zÄÖÜäöüéèêàâçß\\-]*';
// JS-\b kennt keine Umlaute – vor Wörtern mit Ä/É usw. stattdessen dieser Lookbehind.
const NB = '(?<![0-9A-Za-z_ÄÖÜäöüßéèêàâç])';

const EMAIL_RX = /[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}/g;

const IBAN_RX = /\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]){11,30}\b/g;

// BIC/SWIFT-Code (8 oder 11 Zeichen: 4 Buchstaben + 2 Buchstaben + 2 alphanumerische
// + optional 3 alphanumerische). Nur mit vorangehendem Schlüsselwort (BIC, SWIFT,
// "SWIFT-Code"), um Fehltreffer zu vermeiden; nur der Code selbst wird ersetzt.
const BIC_RX = new RegExp(
    '\\b(?:BIC|SWIFT)(?:[\\s\\-]?Code)?[\\s:\\-]+' +
    '(?<bic>[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}(?:[A-Z0-9]{3})?)\\b',
    'dg');

// Schweizer AHV-Nummer (756.1234.5678.97) und US Social Security Number (123-45-6789)
const SSN_RX = /\b756[.\s]?\d{4}[.\s]?\d{4}[.\s]?\d{2}\b|\b\d{3}-\d{2}-\d{4}\b/g;

// Schweizer Versichertenkarte (Krankenkasse): 20-stellige Nummer, beginnt mit 807.
const HEALTH_CARD_RX = /(?<!\d)807\d{17}(?!\d)/g;

// Schweizer Unternehmens-Identifikationsnummer (UID): CHE-123.456.789
const UID_RX = /\bCHE-\d{3}\.\d{3}\.\d{3}(?:\s?(?:MWST|TVA|IVA|VAT))?\b/g;

// Fahrgestellnummer (VIN): genau 17 Zeichen aus A-H, J-N, P, R-Z und 0-9 (kein I/O/Q).
const VIN_RX = /\b[A-HJ-NPR-Z0-9]{17}\b/g;

// IP-Adressen: IPv4 (0-255) und einfache IPv6.
const IP_RX = /\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b|\b(?:[A-Fa-f0-9]{1,4}:){2,7}[A-Fa-f0-9]{1,4}\b/g;

// Kandidaten für Kreditkarten (13–19 Ziffern), werden zusätzlich per Luhn geprüft.
const CARD_RX = /(?<!\d)\d(?:[ \-]?\d){12,18}(?!\d)/g;

const PHONE_RX = new RegExp(
    '(?:\\+|00)[1-9]\\d{1,2}(?:[ \\-/]?\\(0\\))?(?:[ \\-/]?\\d){6,12}' +      // +41 79 123 45 67
    '|(?<![\\d.])0\\d{2}[ /\\-]?\\d{3}[ /\\-]?\\d{2}[ /\\-]?\\d{2}(?![\\d.])' + // 044 123 45 67
    '|(?<![\\d.])0\\d{3,4}[ /\\-]\\d{4,8}(?![\\d.])' +                        // 0621 1234567
    '|\\(\\d{3}\\)\\s?\\d{3}[-. ]\\d{4}\\b' +                                 // (555) 123-4567
    '|(?<![\\d.\\-])\\d{3}[-.]\\d{3}[-.]\\d{4}(?![\\d.\\-])',                 // 555-123-4567
    'g');

// Numerische Daten: 31.12.1980, 31.12.80, 1980-12-31, 12/31/1980, 31/12/80
const NUMERIC_DATE = '[0-3]?\\d\\.[01]?\\d\\.(?:\\d{4}|\\d{2})|(?:19|20)\\d{2}-[01]\\d-[0-3]\\d|\\d{1,2}/\\d{1,2}/(?:\\d{4}|\\d{2})';

const DATE_RX = new RegExp('\\b(?:' + NUMERIC_DATE + ')\\b', 'g');

const MONTH_NAME =
    '(?:Januar|Februar|März|April|Mai|Juni|Juli|August|September|Oktober|November|Dezember' +
    '|January|February|March|May|June|July|October|December' +
    '|janvier|février|mars|avril|juin|juillet|août|septembre|octobre|novembre|décembre' +
    '|gennaio|febbraio|marzo|aprile|maggio|giugno|luglio|agosto|settembre|ottobre|dicembre)';

// 12. März 1985 / 12 mars 1985 / March 12, 1985
const WRITTEN_DATE = '\\d{1,2}\\.?\\s+' + MONTH_NAME + '\\s+\\d{4}|' + MONTH_NAME + '\\s+\\d{1,2},?\\s+\\d{4}';

const BIRTH_DATE_RX = new RegExp(
    '(?:geb\\.|geboren\\s+am|Geburtsdatum\\s*:?' +
    '|born\\s+on|born\\s*:?|date\\s+of\\s+birth\\s*:?|DOB\\s*:?' +
    '|n[ée]e?\\s+le|date\\s+de\\s+naissance\\s*:?' +
    '|nat[oa]\\s+il|data\\s+di\\s+nascita\\s*:?)\\s*' +
    '(?<d>' + NUMERIC_DATE + '|' + WRITTEN_DATE + ')',
    'dgi');

// Bahnhofstrasse 12a, Rue de Lausanne 12, Via Roma 8, 12 Main Street
const STREET_RX = new RegExp(
    NB + '(?:' + WORD + '\\s+)?' + WORD +
    '(?:strasse|straße|str\\.|weg|gasse|platz|allee|ring|halde|rain|matte?|acker|feld|bühl|hof|damm|ufer|steig|promenade|quai|graben)' +
    '\\s+\\d{1,4}\\s?[a-hA-H]?\\b' +
    '|\\b(?:Rue|Route|Avenue|Av\\.|Chemin|Ch\\.|Via|Viale|Piazza|Place|Boulevard|Bd\\.)' +
    "\\s+(?:de\\s+la\\s+|des\\s+|de\\s+|du\\s+|d')?" + WORD + '(?:\\s+' + WORD + ')?\\s+\\d{1,4}\\b' +
    '|\\b\\d{1,5}\\s+' + WORD + '(?:\\s+' + WORD + ')?\\s+' +
    '(?:Street|St\\.|Road|Rd\\.?|Avenue|Ave\\.?|Lane|Ln\\.?|Drive|Boulevard|Blvd\\.?|Court|Ct\\.?|Way|Square|Terrace|Crescent|Close)(?=[\\s,.;]|$)',
    'g');

// 8001 Zürich, 79576 Weil am Rhein, 1201 Genève
const PLZ_ORT_RX = new RegExp('\\b\\d{4,5}\\s+(?<ort>' + WORD + '(?:\\s+(?:am|an\\s+der|bei|ob|im)\\s+' + WORD + '|\\s+[A-Z]{2}\\b)?)', 'g');

// Herr Max Muster, Mr. John Smith, Madame Claire Dubois, Sig. Mario Rossi
const SALUTATION_NAME_RX = new RegExp(
    '\\b(?:Herrn?|Frau|Hr\\.|Fr\\.' +
    '|Mrs\\.?|Mr\\.?|Ms\\.?|Miss' +
    '|Monsieur|Madame|Mademoiselle|Mme|Mlle|M\\.' +
    '|Signora|Signorina|Signor|Sig\\.ra|Sig\\.|Dott\\.ssa|Dott\\.' +
    '|Dr\\.|Prof\\.)' +
    '\\s+(?:Dr\\.\\s*(?:med\\.\\s*)?|Prof\\.\\s*)?(?<name>' + WORD + '(?:\\s+' + WORD + '){0,2})\\b',
    'dg');

// "Name: Max Muster", "Customer: John Smith", "Client : Jean Dupont", "Cliente: Mario Rossi"
const KEYWORD_NAME_RX = new RegExp(
    '\\b(?:Name|Vorname|Nachname|Kunde|Kundin|Patient(?:in)?|Versicherte[rn]?|Mieter(?:in)?|Mandant(?:in)?|Ansprechpartner(?:in)?|Kontakt' +
    '|Customer|Client(?:e)?|Insured|Tenant|Policyholder|Surname|First\\s+name|Last\\s+name' +
    '|Assur[ée]e?|Locataire|Pr[ée]nom|Nom' +
    '|Paziente|Assicurat[oa]|Inquilin[oa]|Cognome|Nome|Contatto)' +
    '\\s*:\\s*(?<name>' + WORD + '(?:\\s+' + WORD + '){0,3})\\b',
    'dg');

// ZH 123456, AG-345678
const PLATE_RX = /\b(?:AG|AI|AR|BE|BL|BS|FR|GE|GL|GR|JU|LU|NE|NW|OW|SG|SH|SO|SZ|TG|TI|UR|VD|VS|ZG|ZH)[ \-]\d{3,6}\b/g;

// Policen-Nr. 12345, Policy No. P-123, N° de police 4711, Numero polizza 88/99
const REFERENCE_RX = new RegExp(
    '\\b(?:(?:Policen?|Vertrags|Kunden|Fall|Dossier|Schaden|Rechnungs|Versicherten|Mitglieder)[\\- ]?(?:Nr|Nummer|No)\\.?' +
    '|(?:Policy|Contract|Customer|Case|Claim|Invoice|Member|File|Reference)[\\- ]?(?:No|Number|Nr|#)\\.?' +
    '|(?:N[°o]|Num[ée]ro)\\.?\\s*(?:de\\s+)?(?:police|contrat|client|dossier|sinistre|facture|r[ée]f[ée]rence)' +
    '|(?:Police|Contrat|Dossier|Sinistre|Facture|R[ée]f[ée]rence)\\s*(?:n[°o]|num[ée]ro)\\.?' +
    '|(?:Numero|N\\.)\\s*(?:di\\s+)?(?:polizza|contratto|cliente|pratica|sinistro|fattura|riferimento)' +
    '|(?:Polizza|Contratto|Pratica|Sinistro|Fattura|Riferimento)\\s*(?:n[°o.]|numero)\\.?)' +
    '\\s*:?\\s*(?<ref>[A-Za-z0-9][A-Za-z0-9.\\-/]{2,})',
    'dgi');

// Europäische USt-IdNr./VAT, nur mit Schlüsselwort (USt-IdNr., VAT, TVA, N. IVA).
// Nur die Nummer (Ländercode + 8–12 alphanumerische Zeichen) wird ersetzt.
const VAT_RX = new RegExp(
    '\\b(?:USt-?IdNr|VAT|TVA|(?:[NP]\\.?\\s*)?IVA)\\.?\\s*:?\\s*' +
    '(?<vat>[A-Z]{2}[A-Z0-9]{8,12})\\b',
    'dg');

// Wörter, die nach einer Anrede nicht Teil des Namens sind.
const NAME_STOP_WORDS = new Set([
    'Am', 'Im', 'An', 'Auf', 'Aus', 'Bei', 'Der', 'Die', 'Das', 'Den', 'Dem',
    'Ein', 'Eine', 'Und', 'Oder', 'Vom', 'Zum', 'Zur', 'Nach', 'Mit', 'Ist',
    'Hat', 'Wird', 'Kann', 'Soll', 'Sein', 'Ihre', 'Ihr', 'Wir', 'Sie', 'Es',
    'The', 'A', 'An', 'And', 'Or', 'Is', 'Was', 'Has', 'Had', 'Will', 'Can', 'On', 'At', 'In',
    'Le', 'La', 'Les', 'Et', 'Ou', 'Il', 'Elle', 'Est', 'Un', 'Une', 'Ce', 'Cette',
    'Lo', 'Gli', 'E', 'O', 'È', 'Ha', 'Sono', 'Questo', 'Questa'
]);

const MONTH_AND_UNIT_WORDS = new Set([
    'januar', 'februar', 'märz', 'april', 'mai', 'juni', 'juli', 'august',
    'september', 'oktober', 'november', 'dezember',
    'january', 'february', 'march', 'may', 'june', 'july', 'october', 'december',
    'janvier', 'février', 'mars', 'avril', 'juin', 'juillet', 'août',
    'septembre', 'octobre', 'novembre', 'décembre',
    'gennaio', 'febbraio', 'marzo', 'aprile', 'maggio', 'giugno', 'luglio',
    'agosto', 'settembre', 'ottobre', 'dicembre',
    'uhr', 'franken', 'rappen', 'euro', 'chf', 'eur', 'usd', 'prozent',
    'personen', 'stück', 'jahre', 'jahren', 'mio', 'mrd', 'millionen',
    'milliarden', 'kilometer', 'meter', 'minuten', 'stunden', 'tage',
    'tagen', 'wochen', 'monate', 'monaten', 'zeichen', 'seiten',
    'dollars', 'pounds', 'percent', 'people', 'years', 'hours', 'minutes',
    'days', 'weeks', 'months', 'miles', 'pages',
    'francs', 'personnes', 'ans', 'heures', 'pour',
    'franchi', 'persone', 'anni', 'ore', 'giorni'
]);

// Wörter, die nach "Kontakt:" o.Ä. stehen können, aber keine Namen sind.
const NOT_A_NAME_WORDS = new Set([
    'tel', 'telefon', 'mobile', 'natel', 'fax', 'mail', 'email', 'e-mail', 'handy', 'siehe', 'unbekannt',
    'phone', 'see', 'unknown', 'none', 'voir', 'aucun', 'inconnu', 'vedi', 'nessuno', 'sconosciuto'
]);

// ---- Öffentliche API ------------------------------------------------------

/**
 * Übernimmt eine bestehende Zuordnungstabelle als Startpunkt: derselbe Wert
 * bekommt wieder denselben Platzhalter, und die Nummerierung läuft weiter
 * (kein [NAME_1] zweimal für verschiedene Personen). Das hält Text, Bild und
 * PDF in EINER gemeinsamen Tabelle zusammen.
 */
function seedFromExisting(existing, placeholderByValue, counters) {
    for (const m of existing ?? []) {
        if (!m || typeof m.placeholder !== 'string' || typeof m.original !== 'string') {
            continue;
        }
        const category = m.category ?? 'term';
        placeholderByValue.set(category + '\u0000' + normalize(m.original), m.placeholder);
        const found = /_(\d+)\]\s*$/.exec(m.placeholder);
        if (found) {
            const n = Number(found[1]);
            if (Number.isFinite(n)) {
                counters.set(category, Math.max(counters.get(category) ?? 0, n));
            }
        }
    }
}

/**
 * Anonymisiert den Text. options siehe defaultOptions(); llmFindings ist eine
 * optionale Liste { text, category } vom lokalen LLM. Bei Überschneidungen
 * gewinnen die präziseren Regex-Treffer.
 *
 * options.existingMappings (optional): bereits vergebene Zuordnungen. Gleiche
 * Werte behalten ihren Platzhalter, neue zählen weiter – so passen Text-, Bild-
 * und PDF-Ergebnisse in eine gemeinsame Tabelle.
 */
export function anonymize(text, options, llmFindings = null) {
    if (!text) {
        return { anonymizedText: '', mappings: [] };
    }

    let candidates = collect(text, options, llmFindings);

    // Vom Benutzer freigegebene Werte vor der Überlappungs-Auflösung entfernen,
    // damit sie auch keine anderen Treffer verdrängen.
    const allowedTerms = (options.allowedTerms ?? []).filter(t => t && t.trim());
    if (allowedTerms.length > 0) {
        const allowed = new Set(allowedTerms.map(t => normalize(t).toLowerCase()));
        candidates = candidates.filter(c => !allowed.has(normalize(c.original).toLowerCase()));
    }

    const accepted = resolveOverlaps(candidates);

    const labels = LABELS[options.language] ?? LABELS.de;
    const placeholderByValue = new Map();
    const counters = new Map();
    seedFromExisting(options.existingMappings, placeholderByValue, counters);

    const mappings = [];
    const emitted = new Set();

    let out = '';
    let pos = 0;
    for (const c of accepted.sort((a, b) => a.start - b.start)) {
        const key = c.category + '\u0000' + normalize(c.original);
        let placeholder = placeholderByValue.get(key);
        if (placeholder === undefined) {
            const n = (counters.get(c.category) ?? 0) + 1;
            counters.set(c.category, n);
            placeholder = `[${labels[c.category]}_${n}]`;
            placeholderByValue.set(key, placeholder);
        }
        // Jede in DIESEM Durchgang genutzte Zuordnung einmal zurueckgeben – auch
        // wiederverwendete, damit der Aufrufer die passende Tabelle bekommt.
        if (!emitted.has(placeholder)) {
            emitted.add(placeholder);
            mappings.push({ placeholder, original: c.original, category: c.category });
        }
        out += text.slice(pos, c.start) + placeholder;
        pos = c.start + c.length;
    }
    out += text.slice(pos);

    return { anonymizedText: out, mappings };
}

/**
 * Ersetzt Platzhalter in einem Text (z.B. einer KI-Antwort oder einem von der KI
 * erzeugten SQL-Skript) wieder durch die Originalwerte. Tolerant gegenüber
 * Umformatierungen durch KI-Tools: [ name_1 ] oder [Name_1] werden auch erkannt.
 */
export function deanonymize(text, mappings) {
    if (!text) {
        return '';
    }
    if (!mappings || mappings.length === 0) {
        return text;
    }
    // Nachschlagetabelle: innerer Platzhaltername (ohne Klammern, ohne Gross-/Kleinschreibung)
    // → Originalwert. Danach EIN Durchgang über den Text statt ein Regex je Platzhalter.
    const byInner = new Map();
    for (const m of mappings) {
        if (!m || typeof m.placeholder !== 'string') {
            continue;
        }
        const inner = m.placeholder.replace(/^\[|\]$/g, '').trim().toLowerCase();
        if (inner && !byInner.has(inner)) {
            byInner.set(inner, m.original ?? '');
        }
    }
    // Funktions-Ersetzung: der Rückgabewert wird wörtlich eingesetzt, $-Zeichen sind unkritisch.
    return text.replace(/\[\s*([^[\]]+?)\s*\]/g, (whole, inner) => {
        const hit = byInner.get(inner.trim().toLowerCase());
        return hit !== undefined ? hit : whole;
    });
}

// ---- Interne Logik --------------------------------------------------------

function normalize(value) {
    return value.trim().replace(/\s+/g, ' ');
}

function escapeRegex(value) {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function collect(text, options, llmFindings) {
    const result = [];
    let priority = 0;

    // Eigene Begriffe haben die höchste Priorität.
    for (const term of options.customTerms ?? []) {
        const trimmed = (term ?? '').trim();
        if (!trimmed) {
            continue;
        }
        const rx = new RegExp(escapeRegex(trimmed), 'gi');
        addMatches(result, text, rx, 'term', priority);
    }

    priority++;
    if (options.emails) {
        addMatches(result, text, EMAIL_RX, 'email', priority);
    }

    priority++;
    if (options.iban) {
        addMatches(result, text, IBAN_RX, 'iban', priority);
        // BIC/SWIFT-Codes (nur mit Schlüsselwort) – gleiche Kategorie wie IBAN.
        for (const m of text.matchAll(BIC_RX)) {
            const g = m.groups?.bic;
            if (g) {
                const start = m.indices.groups.bic[0];
                result.push({ start, length: g.length, original: g, category: 'iban', priority });
            }
        }
    }

    priority++;
    if (options.ssn) {
        addMatches(result, text, SSN_RX, 'ssn', priority);
        addMatches(result, text, HEALTH_CARD_RX, 'ssn', priority);
    }

    priority++;
    if (options.ip) {
        addMatches(result, text, IP_RX, 'ip', priority);
    }

    priority++;
    if (options.cards) {
        for (const m of text.matchAll(CARD_RX)) {
            if (isLuhnValid(m[0])) {
                result.push({ start: m.index, length: m[0].length, original: m[0], category: 'card', priority });
            }
        }
    }

    priority++;
    if (options.refs) {
        addMatches(result, text, UID_RX, 'ref', priority);
        // Fahrgestellnummer (VIN): der ganze Treffer ist die Nummer.
        addMatches(result, text, VIN_RX, 'ref', priority);
        // Europäische USt-IdNr./VAT (nur mit Schlüsselwort) – nur die Nummer ersetzen.
        for (const m of text.matchAll(VAT_RX)) {
            const g = m.groups?.vat;
            if (g) {
                const start = m.indices.groups.vat[0];
                result.push({ start, length: g.length, original: g, category: 'ref', priority });
            }
        }
        for (const m of text.matchAll(REFERENCE_RX)) {
            const g = m.groups?.ref;
            if (!g) {
                continue;
            }
            // Satzzeichen am Ende gehören nicht zur Nummer ("Fall-Nr: 88123.").
            const value = g.replace(/[.\-/]+$/, '');
            if (value.length >= 3) {
                const start = m.indices.groups.ref[0];
                result.push({ start, length: value.length, original: value, category: 'ref', priority });
            }
        }
    }

    priority++;
    if (options.phones) {
        addMatches(result, text, PHONE_RX, 'phone', priority);
    }

    priority++;
    if (options.allDates) {
        addMatches(result, text, DATE_RX, 'date', priority);
    }
    if (options.birthdays || options.allDates) {
        for (const m of text.matchAll(BIRTH_DATE_RX)) {
            const g = m.groups?.d;
            if (g) {
                const start = m.indices.groups.d[0];
                result.push({ start, length: g.length, original: g, category: 'date', priority });
            }
        }
    }

    priority++;
    if (options.streets) {
        addMatches(result, text, STREET_RX, 'address', priority);
    }

    priority++;
    if (options.cities) {
        for (const m of text.matchAll(PLZ_ORT_RX)) {
            const ort = (m.groups?.ort ?? '').split(' ')[0];
            if (!MONTH_AND_UNIT_WORDS.has(ort.toLowerCase())) {
                result.push({ start: m.index, length: m[0].length, original: m[0], category: 'city', priority });
            }
        }
    }

    priority++;
    if (options.plates) {
        for (const m of text.matchAll(PLATE_RX)) {
            // "SG 2024" ist eher eine Jahreszahl als ein Kennzeichen.
            const digits = m[0].slice(3).trim();
            if (digits.length === 4 && (digits.startsWith('19') || digits.startsWith('20'))) {
                continue;
            }
            result.push({ start: m.index, length: m[0].length, original: m[0], category: 'plate', priority });
        }
    }

    priority++;
    if (options.names) {
        addNameMatches(result, text, SALUTATION_NAME_RX, priority);
        addNameMatches(result, text, KEYWORD_NAME_RX, priority);
    }

    // LLM-Funde zuletzt: Regex-Treffer sind bei Überschneidungen präziser.
    priority++;
    if (llmFindings && llmFindings.length > 0) {
        addLlmCandidates(result, text, llmFindings, options, priority);
    }

    return result;
}

function addMatches(result, text, rx, category, priority) {
    for (const m of text.matchAll(rx)) {
        result.push({ start: m.index, length: m[0].length, original: m[0], category, priority });
    }
}

function addNameMatches(result, text, rx, priority) {
    for (const m of text.matchAll(rx)) {
        const g = m.groups?.name;
        if (!g) {
            continue;
        }

        // Nachlaufende Wörter abschneiden, die keine Namen sind ("Herr Müller Am Montag ...").
        const words = g.split(' ');
        let keep = words.length;
        while (keep > 1 && NAME_STOP_WORDS.has(words[keep - 1])) {
            keep--;
        }
        const value = words.slice(0, keep).join(' ');
        if (keep === 1 && (NAME_STOP_WORDS.has(value) || NOT_A_NAME_WORDS.has(value.toLowerCase()))) {
            continue;
        }
        const start = m.indices.groups.name[0];
        result.push({ start, length: value.length, original: value, category: 'name', priority });
    }
}

const LLM_CATEGORY_ENABLED = {
    name: o => o.names,
    email: o => o.emails,
    phone: o => o.phones,
    address: o => o.streets,
    city: o => o.cities,
    iban: o => o.iban,
    ssn: o => o.ssn,
    card: o => o.cards,
    ref: o => o.refs,
    plate: o => o.plates,
    org: o => o.orgs,
    ip: o => o.ip,
    sensitive: o => o.sensitive,
    date: o => o.birthdays || o.allDates
};

function addLlmCandidates(result, text, findings, options, priority) {
    for (const finding of findings) {
        const value = (finding.text ?? '').trim();
        if (value.length < 2 || value.includes('[') || value.includes(']')) {
            continue;
        }
        const enabled = LLM_CATEGORY_ENABLED[finding.category];
        if (enabled && !enabled(options)) {
            continue;
        }

        if (!addOccurrences(result, text, value, finding.category, priority, false)) {
            // Das LLM gibt den Text nicht immer buchstabengetreu zurück –
            // zweiter Versuch ohne Gross-/Kleinschreibung.
            addOccurrences(result, text, value, finding.category, priority, true);
        }
    }
}

function addOccurrences(result, text, value, category, priority, ignoreCase) {
    const haystack = ignoreCase ? text.toLowerCase() : text;
    const needle = ignoreCase ? value.toLowerCase() : value;
    let found = false;
    let idx = 0;
    while ((idx = haystack.indexOf(needle, idx)) >= 0) {
        result.push({ start: idx, length: value.length, original: text.slice(idx, idx + value.length), category, priority });
        found = true;
        idx += value.length;
    }
    return found;
}

/** Bei Überschneidungen gewinnt die Regel mit der höheren Priorität, danach der längere Treffer. */
function resolveOverlaps(candidates) {
    const accepted = [];
    const sorted = candidates.sort((a, b) =>
        a.priority - b.priority || b.length - a.length || a.start - b.start);
    for (const c of sorted) {
        const overlaps = accepted.some(a => c.start < a.start + a.length && a.start < c.start + c.length);
        if (!overlaps) {
            accepted.push(c);
        }
    }
    return accepted;
}

function isLuhnValid(value) {
    const digits = [...value].filter(ch => ch >= '0' && ch <= '9').map(Number);
    if (digits.length < 13 || digits.length > 19) {
        return false;
    }
    let sum = 0;
    let doubleIt = false;
    for (let i = digits.length - 1; i >= 0; i--) {
        let d = digits[i];
        if (doubleIt) {
            d *= 2;
            if (d > 9) {
                d -= 9;
            }
        }
        sum += d;
        doubleIt = !doubleIt;
    }
    return sum % 10 === 0;
}
