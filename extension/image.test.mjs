// Tests für die reine Bild-Redaktionslogik (planImageRedaction). Kein echtes
// OCR nötig – wir füttern synthetische OCR-Wörter mit Positionen.
// Ausführen: node extension/image.test.mjs

import { planImageRedaction, parseTsvWords } from './image.js';
import { defaultOptions } from './engine.js';

let failures = 0;
function check(name, ok) {
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}`);
    if (!ok) failures++;
}

// Synthetische OCR-Ausgabe: "Herr Max Muster max.muster@example.ch 192.168.0.5"
const box = (x0) => ({ x0, y0: 10, x1: x0 + 40, y1: 30 });
const words = [
    { text: 'Herr', bbox: box(0) },
    { text: 'Max', bbox: box(50) },
    { text: 'Muster', bbox: box(100) },
    { text: 'max.muster@example.ch', bbox: box(160) },
    { text: '192.168.0.5', bbox: box(320) }
];

const plan = planImageRedaction(words, { ...defaultOptions(), language: 'de' });

// "Herr Max Muster" -> Name (nach Anrede), E-Mail und IP werden erkannt.
const redactedTexts = plan.boxes.map(b => {
    // Finde das zugehörige Wort über die x0-Position.
    return words.find(w => w.bbox.x0 === b.x0)?.text;
});

check('E-Mail-Wort wird geschwärzt', redactedTexts.includes('max.muster@example.ch'));
check('IP-Wort wird geschwärzt', redactedTexts.includes('192.168.0.5'));
check('Name (Max/Muster) wird geschwärzt', redactedTexts.includes('Max') && redactedTexts.includes('Muster'));
check('Anrede "Herr" bleibt sichtbar', !redactedTexts.includes('Herr'));
check('Boxen tragen die Kategorie', plan.boxes.every(b => typeof b.category === 'string'));
check('E-Mail-Box hat Kategorie email', plan.boxes.find(b => b.x0 === 160)?.category === 'email');
check('IP-Box hat Kategorie ip', plan.boxes.find(b => b.x0 === 320)?.category === 'ip');

// Bild ohne PII -> keine Boxen.
const clean = planImageRedaction([
    { text: 'Rechnung', bbox: box(0) },
    { text: 'Betrag', bbox: box(50) },
    { text: '100', bbox: box(100) }
], defaultOptions());
check('Kein PII -> keine Boxen', clean.boxes.length === 0);

// Erlaubte Werte werden auch im Bild nicht geschwärzt.
const allowPlan = planImageRedaction(words, { ...defaultOptions(), language: 'de', allowedTerms: ['192.168.0.5'] });
const allowTexts = allowPlan.boxes.map(b => words.find(w => w.bbox.x0 === b.x0)?.text);
check('Erlaubter Wert (IP) bleibt im Bild sichtbar', !allowTexts.includes('192.168.0.5'));

// --- TSV-Parser (Tesseract-Ausgabe) ---
const tsv = [
    '1\t1\t0\t0\t0\t0\t0\t0\t760\t120\t-1\t',        // Seite
    '4\t1\t1\t1\t1\t0\t15\t22\t230\t20\t-1\t',        // Zeile (kein Wort)
    '5\t1\t1\t1\t1\t1\t15\t22\t58\t20\t95.7\tHerr',   // Wort
    '5\t1\t1\t1\t1\t2\t85\t22\t53\t20\t95.9\tMax',    // Wort
    '5\t1\t1\t1\t1\t3\t150\t22\t80\t20\t12.0\tMxxx',  // niedrige Konfidenz -> raus
    ''
].join('\n');
const parsed = parseTsvWords(tsv);
check('TSV: nur Wortzeilen (level 5) mit genug Konfidenz', parsed.length === 2);
check('TSV: Text korrekt', parsed[0].text === 'Herr' && parsed[1].text === 'Max');
check('TSV: Bounding-Box aus left/top/width/height', parsed[0].bbox.x0 === 15 && parsed[0].bbox.y0 === 22 && parsed[0].bbox.x1 === 73 && parsed[0].bbox.y1 === 42);
check('TSV: leere Eingabe -> keine Wörter', parseTsvWords('').length === 0);

console.log();
console.log(failures === 0 ? 'ALLE BILD-TESTS BESTANDEN' : `${failures} TEST(S) FEHLGESCHLAGEN`);
process.exit(failures === 0 ? 0 : 1);
