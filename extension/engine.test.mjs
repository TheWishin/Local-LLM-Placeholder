// Tests für die JS-Engine der Browser-Erweiterung – spiegelt die Checks aus
// tests/DataAnonymizer.Tests/Program.cs. Ausführen mit: node extension/engine.test.mjs

import { anonymize, deanonymize, defaultOptions, labelFor } from './engine.js';
import { parseEntities } from './ollama.js';

let failures = 0;
function check(name, ok) {
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}`);
    if (!ok) failures++;
}

// --- Deutsch (Basisfall aus den C#-Tests) ---
const text = `Schadenmeldung Muster AG

Herr Max Muster (geb. 12.03.1985), wohnhaft Bahnhofstrasse 12a, 8001 Zürich,
meldet am 15.06.2026 einen Wasserschaden. Kontakt: Tel. +41 79 123 45 67 oder
044 123 45 67, E-Mail max.muster@example.ch.

Kundin: Anna Meier-Huber, Rue de Lausanne 12, 1201 Genève.
AHV-Nr. 756.1234.5678.97, IBAN CH93 0076 2011 6238 5295 7.
Policen-Nr. P-2023/4711, Fall-Nr: 88123.
Fahrzeug ZH 456789. Kreditkarte 4539 1488 0343 6467.
Frau Meier-Huber und Herr Muster sind erreichbar.
Die Muster AG bestätigt den Eingang.`;

const opts = { ...defaultOptions(), customTerms: ['Muster AG'] };
const result = anonymize(text, opts);
const a = result.anonymizedText;

check('E-Mail entfernt', !a.includes('max.muster@example.ch') && a.includes('[EMAIL_1]'));
check('IBAN entfernt', !a.includes('CH93') && a.includes('[IBAN_1]'));
check('AHV entfernt', !a.includes('756.1234.5678.97') && a.includes('[AHV_1]'));
check('Mobilnummer entfernt', !a.includes('+41 79 123 45 67'));
check('Festnetz entfernt', !a.includes('044 123 45 67'));
check('Strasse entfernt', !a.includes('Bahnhofstrasse 12a') && a.includes('[ADRESSE_1]'));
check('Rue de Lausanne entfernt', !a.includes('Rue de Lausanne 12'));
check('PLZ/Ort entfernt', !a.includes('8001 Zürich') && !a.includes('1201 Genève'));
check('Name nach Herr entfernt', !a.includes('Max Muster'));
check('Name nach Kundin: entfernt', !a.includes('Anna Meier-Huber'));
check('Gleicher Name = gleicher Platzhalter', a.includes('Herr [NAME_1]') && a.includes('Frau [NAME_'));
check('Geburtsdatum entfernt', !a.includes('12.03.1985') && a.includes('[DATUM_1]'));
check('Normales Datum bleibt (allDates=false)', a.includes('15.06.2026'));
check('Policen-Nr. entfernt', !a.includes('P-2023/4711'));
check('Fall-Nr. entfernt', !a.includes('88123'));
check('Kennzeichen entfernt', !a.includes('ZH 456789'));
check('Kreditkarte entfernt (Luhn ok)', !a.includes('4539 1488 0343 6467') && a.includes('[KARTE_1]'));
check('Eigener Begriff entfernt', !a.includes('Muster AG') && a.includes('[BEGRIFF_1]'));
check('Round-Trip stellt Originaltext wieder her', deanonymize(a, result.mappings) === text);
check("'Tel' wird nicht als Name erkannt", !result.mappings.some(m => m.original === 'Tel'));
check('Referenz ohne Satzpunkt', result.mappings.some(m => m.original === '88123') && !result.mappings.some(m => m.original === '88123.'));

// --- Englisch ---
const enText = `Mr. John Smith (born on 12/03/1985), 12 Main Street, reports a claim.
Customer: Jane Miller, phone (555) 123-4567 or 555-987-6543, SSN 078-05-1120.
Policy No. P-2023/4711, Case #88123. Dr. Brown will call back.`;
const en = anonymize(enText, { ...defaultOptions(), language: 'en' });
const ea = en.anonymizedText;
check('EN: Name nach Mr. entfernt', !ea.includes('John Smith') && ea.includes('Mr. [NAME_1]'));
check('EN: Name nach Customer: entfernt', !ea.includes('Jane Miller'));
check('EN: Name nach Dr. entfernt', !ea.includes('Brown'));
check('EN: Geburtsdatum (born on) entfernt', !ea.includes('12/03/1985'));
check('EN: US-Telefonnummern entfernt', !ea.includes('(555) 123-4567') && !ea.includes('555-987-6543'));
check('EN: US-SSN entfernt', !ea.includes('078-05-1120') && ea.includes('[SSN_1]'));
check('EN: Strasse entfernt', !ea.includes('12 Main Street') && ea.includes('[ADDRESS_1]'));
check('EN: Policy-Nr. entfernt', !ea.includes('P-2023/4711'));
check('EN: Case # entfernt', !ea.includes('88123'));
check('EN: Round-Trip', deanonymize(ea, en.mappings) === enText);

// --- Französisch ---
const frText = `Monsieur Jean Dupont, né le 12.03.1985, habite Rue de Lausanne 12, 1201 Genève.
Client : Pierre Martin. N° de police 4711. Mme Claire Dubois est informée.`;
const fr = anonymize(frText, { ...defaultOptions(), language: 'fr' });
const fa = fr.anonymizedText;
check('FR: Name nach Monsieur entfernt', !fa.includes('Jean Dupont') && fa.includes('Monsieur [NOM_1]'));
check('FR: Name nach Client : entfernt', !fa.includes('Pierre Martin'));
check('FR: Name nach Mme entfernt', !fa.includes('Claire Dubois'));
check('FR: Geburtsdatum (né le) entfernt', !fa.includes('12.03.1985'));
check('FR: Adresse entfernt', !fa.includes('Rue de Lausanne 12'));
check('FR: PLZ/Ort entfernt', !fa.includes('1201 Genève'));
check('FR: Policen-Nr. entfernt', !fa.includes('4711'));
check('FR: Round-Trip', deanonymize(fa, fr.mappings) === frText);

// --- Italienisch ---
const itText = `Signor Mario Rossi, nato il 12.03.1985, abita in Via Roma 8, 6900 Lugano.
Cliente: Anna Bianchi. Polizza n. 4711. La Sig.ra Bianchi è informata.`;
const it = anonymize(itText, { ...defaultOptions(), language: 'it' });
const ia = it.anonymizedText;
check('IT: Name nach Signor entfernt', !ia.includes('Mario Rossi') && ia.includes('Signor [NOME_1]'));
check('IT: Name nach Cliente: entfernt', !ia.includes('Anna Bianchi'));
check('IT: Geburtsdatum (nato il) entfernt', !ia.includes('12.03.1985'));
check('IT: Adresse entfernt', !ia.includes('Via Roma 8'));
check('IT: PLZ/Ort entfernt', !ia.includes('6900 Lugano'));
check('IT: Polizza-Nr. entfernt', !ia.includes('4711'));
check('IT: Round-Trip', deanonymize(ia, it.mappings) === itText);

// --- Ausgeschriebene Geburtsdaten ---
const wd = anonymize('Mrs. Smith was born on March 12, 1985. Herr Muster, geboren am 12. März 1985.', defaultOptions());
check("Geburtsdatum 'March 12, 1985' entfernt", !wd.anonymizedText.includes('March 12, 1985'));
check("Geburtsdatum '12. März 1985' entfernt", !wd.anonymizedText.includes('12. März 1985'));

// --- Platzhalter-Sprache ---
check('Platzhalter Deutsch ([TELEFON_1])', anonymize('Tel. +41 79 123 45 67', defaultOptions()).anonymizedText.includes('[TELEFON_1]'));
check('Platzhalter Englisch ([PHONE_1])', anonymize('Tel. +41 79 123 45 67', { ...defaultOptions(), language: 'en' }).anonymizedText.includes('[PHONE_1]'));
check('labelFor liefert Sprachlabel', labelFor('phone', 'fr') === 'TELEPHONE' && labelFor('name', 'it') === 'NOME');

// --- Sonderfälle ---
const r2 = anonymize('Termin am 15.06.2026 um 14 Uhr, geboren am 01.01.1990.', { ...defaultOptions(), allDates: true });
check('allDates ersetzt beide Daten', !r2.anonymizedText.includes('15.06.2026') && !r2.anonymizedText.includes('01.01.1990'));
const r3 = anonymize('Im Jahr 2024 Januar gab es 1500 Franken Umsatz.', defaultOptions());
check("Kein falscher Ort bei '2024 Januar'", !r3.anonymizedText.includes('[ORT'));
const r4 = anonymize('', defaultOptions());
check('Leerer Text ok', r4.anonymizedText === '' && r4.mappings.length === 0);
const r5 = anonymize('Betrag 1234 5678 9012 3456 Rappen.', defaultOptions());
check('Ungültige Kartennummer (Luhn) bleibt', r5.anonymizedText.includes('1234 5678 9012 3456'));

// --- LLM-Funde ---
const llmText = 'Max Muster arbeitet bei der Contoso AG in Zürich. E-Mail max.muster@example.ch.';
const llmFindings = [
    { text: 'Max Muster', category: 'name' },
    { text: 'Contoso AG', category: 'org' },
    { text: 'max.muster@example.ch', category: 'email' },
    { text: 'Nicht Im Text', category: 'name' },
    { text: 'zürich', category: 'city' }
];
const llm = anonymize(llmText, defaultOptions(), llmFindings);
const la = llm.anonymizedText;
check('LLM: Name ohne Anrede entfernt', !la.includes('Max Muster') && la.includes('[NAME_1]'));
check('LLM: Firma entfernt', !la.includes('Contoso AG') && la.includes('[FIRMA_1]'));
check('LLM: E-Mail nur einmal ersetzt', la.includes('[EMAIL_1]') && llm.mappings.filter(m => m.category === 'email').length === 1);
check('LLM: Unbekannter Fund ignoriert', !llm.mappings.some(m => m.original === 'Nicht Im Text'));
check('LLM: Fund trotz anderer Schreibweise', !la.includes('Zürich'));
check('LLM: Round-Trip', deanonymize(la, llm.mappings) === llmText);

const llmOff = anonymize(llmText, { ...defaultOptions(), names: false, orgs: false }, llmFindings);
check('LLM: Deaktivierte Kategorien übersprungen', llmOff.anonymizedText.includes('Max Muster') && llmOff.anonymizedText.includes('Contoso AG'));

// --- Erlaubte Werte (Allowlist) ---
const allowText = 'Herr Max Muster wohnt in 8001 Zürich, Frau Anna Meier auch. Tel. +41 79 123 45 67.';
const allow = anonymize(allowText, { ...defaultOptions(), allowedTerms: ['8001  zürich', 'Max Muster'] });
check('Allow: Erlaubter Name bleibt sichtbar', allow.anonymizedText.includes('Max Muster'));
check('Allow: Erlaubter Ort bleibt (case-insensitiv, Leerraum egal)', allow.anonymizedText.includes('8001 Zürich'));
check('Allow: Andere Namen werden weiter ersetzt', !allow.anonymizedText.includes('Anna Meier'));
check('Allow: Andere Kategorien unberührt', !allow.anonymizedText.includes('+41 79 123 45 67'));

const allowLlm = anonymize('Max Muster arbeitet bei der Contoso AG.',
    { ...defaultOptions(), allowedTerms: ['Contoso AG'] },
    [{ text: 'Max Muster', category: 'name' }, { text: 'Contoso AG', category: 'org' }]);
check('Allow: Gilt auch für LLM-Funde', allowLlm.anonymizedText.includes('Contoso AG') && !allowLlm.anonymizedText.includes('Max Muster'));

// --- Parsen der LLM-Antwort (ollama.js) ---
const parsed = parseEntities('{"entities":[{"text":"Max Muster","category":"name"},{"text":"Contoso AG","category":"organization"},{"text":"Max Muster","category":"name"}]}');
check('Parse: Entitäten gelesen und dedupliziert',
    parsed.length === 2 && parsed[0].text === 'Max Muster' && parsed[0].category === 'name'
    && parsed[1].text === 'Contoso AG' && parsed[1].category === 'org');
const fenced = parseEntities('Here is the result:\n```json\n{"entities":[{"text":"Anna","category":"person"}]}\n```');
check('Parse: JSON in Code-Fence', fenced.length === 1 && fenced[0].category === 'name');
check('Parse: Unbekannte Kategorie wird term', parseEntities('{"entities":[{"text":"X1","category":"whatever"}]}')[0].category === 'term');
check('Parse: Müll ergibt leere Liste', parseEntities('no json here').length === 0);
check('Parse: Kaputtes JSON ergibt leere Liste', parseEntities('{"entities":[{').length === 0);

console.log();
console.log(failures === 0 ? 'ALLE TESTS BESTANDEN' : `${failures} TEST(S) FEHLGESCHLAGEN`);
process.exit(failures === 0 ? 0 : 1);
