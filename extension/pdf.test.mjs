// Tests für die reine PDF-Logik (ohne Browser): mat3, pdfItemsToWords, imagesToPdf.
// Ausführen: node extension/pdf.test.mjs

import { mat3, pdfItemsToWords, imagesToPdf } from './pdf.js';
import { planImageRedaction } from './image.js';
import { defaultOptions } from './engine.js';

let failures = 0;
function check(name, ok) {
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}`);
    if (!ok) failures++;
}
const approx = (a, b) => Math.abs(a - b) < 1e-6;

// --- mat3 ---
const id = [1, 0, 0, 1, 0, 0];
const m = [2, 3, 4, 5, 6, 7];
check('mat3: Identität ist neutral', JSON.stringify(mat3(id, m)) === JSON.stringify(m));
// Verschiebung kombiniert
check('mat3: Translation summiert', JSON.stringify(mat3([1, 0, 0, 1, 10, 20], [1, 0, 0, 1, 5, 5])) === JSON.stringify([1, 0, 0, 1, 15, 25]));

// --- pdfItemsToWords ---
// viewport scale=2, Seitenhöhe 100 → transform [2,0,0,-2,0,200] (y gespiegelt).
const vp = [2, 0, 0, -2, 0, 200];
const items = [
    { str: 'Max', transform: [12, 0, 0, 12, 10, 50], width: 30 },
    { str: '   ', transform: [12, 0, 0, 12, 0, 0], width: 5 },   // nur Leerraum → ignoriert
    { str: 'Muster', transform: [12, 0, 0, 12, 40, 50], width: 40 }
];
const words = pdfItemsToWords(items, vp, 2);
check('pdfItemsToWords: Leerraum-Element übersprungen', words.length === 2);
const w0 = words[0];
check('pdfItemsToWords: Text übernommen', w0.text === 'Max');
check('pdfItemsToWords: x0 korrekt', approx(w0.bbox.x0, 20));
check('pdfItemsToWords: Breite skaliert (x1)', approx(w0.bbox.x1, 80));
check('pdfItemsToWords: Oberkante y0 = Grundlinie - Höhe', approx(w0.bbox.y0, 76));
check('pdfItemsToWords: Unterkante y1 = Grundlinie', approx(w0.bbox.y1, 100));

// --- imagesToPdf ---
const jpegA = new Uint8Array([0xFF, 0xD8, 0xFF, 0xAA, 0xBB, 0xFF, 0xD9]);
const jpegB = new Uint8Array([0xFF, 0xD8, 0x01, 0x02, 0xFF, 0xD9]);
const pdf = imagesToPdf([
    { jpeg: jpegA, width: 200, height: 100 },
    { jpeg: jpegB, width: 50, height: 60 }
]);
const asStr = Buffer.from(pdf).toString('latin1');

check('imagesToPdf: PDF-Kopf', asStr.startsWith('%PDF-1.4'));
check('imagesToPdf: endet mit %%EOF', asStr.trimEnd().endsWith('%%EOF'));
check('imagesToPdf: zwei Seiten (Count)', asStr.includes('/Count 2'));
check('imagesToPdf: zwei Seiten-Objekte', (asStr.match(/\/Type \/Page \/Parent/g) || []).length === 2);
check('imagesToPdf: zwei Bilder (DCTDecode)', (asStr.match(/\/Filter \/DCTDecode/g) || []).length === 2);
check('imagesToPdf: MediaBox der ersten Seite', asStr.includes('/MediaBox [0 0 200 100]'));
check('imagesToPdf: JPEG-Bytes eingebettet', asStr.includes('\xFF\xD8\xFF\xAA'));

// startxref zeigt korrekt auf die xref-Tabelle
const sx = asStr.match(/startxref\n(\d+)\n%%EOF/);
check('imagesToPdf: startxref vorhanden', !!sx);
if (sx) {
    const off = Number(sx[1]);
    check('imagesToPdf: startxref-Offset zeigt auf "xref"', asStr.slice(off, off + 4) === 'xref');
}

// Einzelseite
const one = imagesToPdf([{ jpeg: jpegB, width: 10, height: 10 }]);
check('imagesToPdf: Einzelseite hat Count 1', Buffer.from(one).toString('latin1').includes('/Count 1'));

// --- Platzhalter-Durchgängigkeit über Seiten hinweg -----------------------
// redactPdf braucht einen Browser; die entscheidende Logik (fortlaufende
// Zuordnung) ist die von planImageRedaction + existingMappings. Hier wird
// geprüft, dass eine "Seite 2" nicht denselben Platzhalter neu vergibt.
{
    const page1Words = [
        { text: 'Herr',   bbox: { x0: 0,  y0: 0, x1: 40,  y1: 20 } },
        { text: 'Max',    bbox: { x0: 50, y0: 0, x1: 90,  y1: 20 } },
        { text: 'Muster', bbox: { x0: 100, y0: 0, x1: 160, y1: 20 } }
    ];
    const page2Words = [
        { text: 'Frau',  bbox: { x0: 0,  y0: 0, x1: 40, y1: 20 } },
        { text: 'Anna',  bbox: { x0: 50, y0: 0, x1: 95, y1: 20 } },
        { text: 'Meier', bbox: { x0: 105, y0: 0, x1: 160, y1: 20 } }
    ];
    const opts = { ...defaultOptions(), language: 'en' };

    const p1 = planImageRedaction(page1Words, opts);
    const p2 = planImageRedaction(page2Words, { ...opts, existingMappings: p1.mappings });

    const ph1 = p1.boxes[0].placeholder;
    const ph2 = p2.boxes[0].placeholder;
    check('PDF: Seite 1 bekommt [NAME_1]', ph1 === '[NAME_1]');
    check('PDF: Seite 2 bekommt einen ANDEREN Platzhalter', ph2 !== ph1);
    check('PDF: Seite 2 zaehlt weiter ([NAME_2])', ph2 === '[NAME_2]');

    // Derselbe Mensch auf beiden Seiten -> derselbe Platzhalter.
    const p2same = planImageRedaction(page1Words, { ...opts, existingMappings: p1.mappings });
    check('PDF: gleiche Person behaelt ihren Platzhalter', p2same.boxes[0].placeholder === ph1);
}

console.log();
console.log(failures === 0 ? 'ALLE PDF-TESTS BESTANDEN' : `${failures} PDF-TEST(S) FEHLGESCHLAGEN`);
process.exit(failures === 0 ? 0 : 1);
