// Tests für die reine Bild-Redaktionslogik (planImageRedaction). Kein echtes
// OCR nötig – wir füttern synthetische OCR-Wörter mit Positionen.
// Ausführen: node extension/image.test.mjs

import { planImageRedaction, parseTsvWords, layoutPlaceholderBox } from './image.js';
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
// "Max Muster" ergibt EIN Kaestchen (der Platzhalter soll nur einmal dastehen),
// das aber beide Woerter abdecken muss.
const nameBox = plan.boxes.find(b => b.category === 'name');
check('Name (Max/Muster) wird ersetzt', !!nameBox && nameBox.x0 <= 50 && nameBox.x1 >= 140);
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


// ---- Platzhalter im Bild (statt nur Schwärzen) ----------------------------
{
    const words = [
        { text: 'Herr',   bbox: { x0: 0,   y0: 0, x1: 40,  y1: 20 } },
        { text: 'Max',    bbox: { x0: 50,  y0: 0, x1: 90,  y1: 20 } },
        { text: 'Muster', bbox: { x0: 100, y0: 0, x1: 160, y1: 20 } }
    ];
    const opts = { ...defaultOptions(), language: 'en' };
    const plan = planImageRedaction(words, opts);

    check('Bild: Platzhalter am Kaestchen', plan.boxes.length > 0 && /^\[NAME_\d+\]$/.test(plan.boxes[0].placeholder));
    check('Bild: Vor-/Nachname zu EINEM Kaestchen verschmolzen', plan.boxes.length === 1);
    check('Bild: Kaestchen deckt beide Woerter ab', plan.boxes[0].x0 === 50 && plan.boxes[0].x1 === 160);
    check('Bild: anonymisierter Text vorhanden', typeof plan.anonymizedText === 'string' && plan.anonymizedText.includes('[NAME_1]'));
    check('Bild: Anrede bleibt im Text', plan.anonymizedText.startsWith('Herr '));

    // Nummerierung setzt eine bestehende Tabelle fort (Text/PDF/Bild teilen sie).
    const existing = [{ placeholder: '[NAME_1]', original: 'Anna Beispiel', category: 'name' }];
    const plan2 = planImageRedaction(words, { ...opts, existingMappings: existing });
    check('Bild: Nummerierung laeuft weiter (kein doppeltes [NAME_1])', plan2.boxes[0].placeholder === '[NAME_2]');

    // Gleicher Wert wie in der bestehenden Tabelle -> gleicher Platzhalter.
    const same = [{ placeholder: '[NAME_7]', original: 'Max Muster', category: 'name' }];
    const plan3 = planImageRedaction(words, { ...opts, existingMappings: same });
    check('Bild: gleicher Wert behaelt seinen Platzhalter', plan3.boxes[0].placeholder === '[NAME_7]');
}

// ---- layoutPlaceholderBox (reine Layout-Rechnung) ------------------------
{
    // Vereinfachte Messung: jedes Zeichen ist 0.6 * Schriftgroesse breit.
    const measure = (t, size) => t.length * size * 0.6;

    const wide = layoutPlaceholderBox({ x0: 10, y0: 10, x1: 200, y1: 30 }, '[NAME_1]', measure, 500);
    check('Layout: Schrift passt in die Hoehe', wide.fontSize > 0 && wide.fontSize <= Math.floor(20 * 0.78) + 4);
    check('Layout: Text passt in die Breite', measure('[NAME_1]', wide.fontSize) <= (wide.x1 - wide.x0));

    // Sehr schmales Kaestchen -> Kaestchen wird nach rechts verbreitert.
    const narrow = layoutPlaceholderBox({ x0: 10, y0: 10, x1: 30, y1: 30 }, '[SENSITIVE_12]', measure, 500);
    check('Layout: schmales Kaestchen wird verbreitert', narrow.x1 > 30);
    check('Layout: bleibt im Bild', narrow.x1 <= 500);

    // Am rechten Rand darf nicht ueber die Leinwand hinausgezeichnet werden.
    const edge = layoutPlaceholderBox({ x0: 480, y0: 0, x1: 495, y1: 20 }, '[EMAIL_3]', measure, 500);
    check('Layout: rechter Rand wird eingehalten', edge.x1 <= 500);
}

console.log();
console.log(failures === 0 ? 'ALLE BILD-TESTS BESTANDEN' : `${failures} BILD-TEST(S) FEHLGESCHLAGEN`);
process.exit(failures === 0 ? 0 : 1);
