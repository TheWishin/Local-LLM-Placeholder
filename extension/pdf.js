// PDF-Anonymisierung: erkennt in einem PDF, WO persönliche Daten stehen, und
// schwärzt diese Stellen – Seite für Seite – und liefert ein neues, geschwärztes
// PDF zurück. Alles läuft lokal im Browser (PDF.js zum Lesen/Rendern, dieselbe
// Erkennung wie beim Text, OCR nur für gescannte Seiten). Kein Upload.
//
// Reine, testbare Logik (Node-Tests): mat3, pdfItemsToWords, imagesToPdf.
// Das Rendern und die OCR brauchen den Browser.

import { planImageRedaction, drawRedacted, ocrWords } from './image.js';

/** URL einer gebündelten PDF.js-Datei. */
function vendorPdf(path) {
    return chrome.runtime.getURL(`vendor/pdfjs/${path}`);
}

/** Prüft, ob die PDF.js-Dateien mitgeliefert wurden (im Release der Fall). */
export async function isPdfAvailable() {
    try {
        const res = await fetch(vendorPdf('pdf.min.mjs'), { method: 'HEAD' });
        return res.ok;
    } catch {
        return false;
    }
}

let pdfjsPromise = null;

/** Lädt (einmalig) PDF.js und richtet den gebündelten Worker ein. */
async function loadPdfjs() {
    if (pdfjsPromise) {
        return pdfjsPromise;
    }
    pdfjsPromise = (async () => {
        const pdfjs = await import(vendorPdf('pdf.min.mjs'));
        pdfjs.GlobalWorkerOptions.workerSrc = vendorPdf('pdf.worker.min.mjs');
        return pdfjs;
    })();
    return pdfjsPromise;
}

/**
 * Multipliziert zwei 2x3-Transformationsmatrizen ([a,b,c,d,e,f]) – zuerst m2,
 * dann m1 (wie PDF.js Util.transform). Reine Funktion.
 */
export function mat3(m1, m2) {
    return [
        m1[0] * m2[0] + m1[2] * m2[1],
        m1[1] * m2[0] + m1[3] * m2[1],
        m1[0] * m2[2] + m1[2] * m2[3],
        m1[1] * m2[2] + m1[3] * m2[3],
        m1[0] * m2[4] + m1[2] * m2[5] + m1[4],
        m1[1] * m2[4] + m1[3] * m2[5] + m1[5]
    ];
}

/**
 * Wandelt PDF.js-Textelemente (mit Position im PDF-Raum) in Wort-Objekte mit
 * Pixel-Bounding-Box um – so, wie sie planImageRedaction (aus image.js) erwartet.
 * @param {{str:string, transform:number[], width:number}[]} items
 * @param {number[]} viewportTransform  viewport.transform (PDF-Raum → Pixel)
 * @param {number} scale                viewport.scale
 */
export function pdfItemsToWords(items, viewportTransform, scale = 1) {
    const words = [];
    for (const item of items || []) {
        const str = item && typeof item.str === 'string' ? item.str : '';
        if (!str.trim() || !Array.isArray(item.transform)) {
            continue;
        }
        const tx = mat3(viewportTransform, item.transform);
        const fontHeight = Math.hypot(tx[2], tx[3]);        // Zeichenhöhe in Pixeln
        const widthPx = (item.width || 0) * scale;
        const x0 = tx[4];
        const baseline = tx[5];                              // Pixel-y der Grundlinie
        words.push({
            text: str,
            bbox: {
                x0,
                y0: baseline - fontHeight,                   // Oberkante
                x1: x0 + widthPx,
                y1: baseline                                 // Unterkante
            }
        });
    }
    return words;
}

/**
 * Baut aus geschwärzten Seiten-Bildern (JPEG) ein einziges PDF – ohne externe
 * Bibliothek. Jede Seite ist ein DCTDecode-Bild in voller Seitengrösse.
 * Reine Funktion (Uint8Array rein/raus), damit testbar.
 * @param {{jpeg:Uint8Array, width:number, height:number}[]} pages
 * @returns {Uint8Array}
 */
export function imagesToPdf(pages) {
    const enc = new TextEncoder();
    const parts = [];
    let length = 0;
    const offsets = {};

    const push = bytes => { parts.push(bytes); length += bytes.length; };
    const pushStr = s => push(enc.encode(s));

    pushStr('%PDF-1.4\n');
    push(new Uint8Array([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A])); // Binär-Markierung

    const n = pages.length;
    const catalogNum = 1;
    const pagesNum = 2;
    const imageNums = [];
    const contentNums = [];
    const pageNums = [];
    let counter = 3;
    for (let i = 0; i < n; i++) {
        imageNums.push(counter++);
        contentNums.push(counter++);
        pageNums.push(counter++);
    }

    const beginObj = num => { offsets[num] = length; pushStr(`${num} 0 obj\n`); };
    const endObj = () => pushStr('endobj\n');

    beginObj(catalogNum);
    pushStr(`<< /Type /Catalog /Pages ${pagesNum} 0 R >>\n`);
    endObj();

    beginObj(pagesNum);
    pushStr(`<< /Type /Pages /Count ${n} /Kids [${pageNums.map(p => `${p} 0 R`).join(' ')}] >>\n`);
    endObj();

    for (let i = 0; i < n; i++) {
        const { jpeg, width, height } = pages[i];

        beginObj(imageNums[i]);
        pushStr(`<< /Type /XObject /Subtype /Image /Width ${width} /Height ${height} ` +
            `/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${jpeg.length} >>\nstream\n`);
        push(jpeg);
        pushStr('\nendstream\n');
        endObj();

        const content = `q ${width} 0 0 ${height} 0 0 cm /Im0 Do Q\n`;
        const contentBytes = enc.encode(content);
        beginObj(contentNums[i]);
        pushStr(`<< /Length ${contentBytes.length} >>\nstream\n`);
        push(contentBytes);
        pushStr('endstream\n');
        endObj();

        beginObj(pageNums[i]);
        pushStr(`<< /Type /Page /Parent ${pagesNum} 0 R /MediaBox [0 0 ${width} ${height}] ` +
            `/Resources << /XObject << /Im0 ${imageNums[i]} 0 R >> >> ` +
            `/Contents ${contentNums[i]} 0 R >>\n`);
        endObj();
    }

    const totalObjs = counter - 1;
    const xrefOffset = length;
    pushStr('xref\n');
    pushStr(`0 ${totalObjs + 1}\n`);
    pushStr('0000000000 65535 f \n');
    for (let num = 1; num <= totalObjs; num++) {
        pushStr(`${String(offsets[num]).padStart(10, '0')} 00000 n \n`);
    }
    pushStr(`trailer\n<< /Size ${totalObjs + 1} /Root ${catalogNum} 0 R >>\n`);
    pushStr(`startxref\n${xrefOffset}\n%%EOF\n`);

    const out = new Uint8Array(length);
    let pos = 0;
    for (const p of parts) {
        out.set(p, pos);
        pos += p.length;
    }
    return out;
}

/** Rendert eine PDF-Seite in ein Canvas. */
async function renderPageToCanvas(page, scale) {
    const viewport = page.getViewport({ scale });
    const canvas = document.createElement('canvas');
    canvas.width = Math.ceil(viewport.width);
    canvas.height = Math.ceil(viewport.height);
    const ctx = canvas.getContext('2d');
    await page.render({ canvasContext: ctx, viewport }).promise;
    return { canvas, viewport };
}

/** Canvas → JPEG-Bytes. */
function canvasToJpeg(canvas, quality = 0.85) {
    return new Promise(resolve => {
        canvas.toBlob(async blob => {
            const buf = await blob.arrayBuffer();
            resolve(new Uint8Array(buf));
        }, 'image/jpeg', quality);
    });
}

/**
 * Anonymisiert ein ganzes PDF: erkennt persönliche Daten, schwärzt sie auf jeder
 * Seite und liefert ein neues, geschwärztes PDF als Blob.
 * @param {Blob|File} file
 * @param {object} options   Erkennungsoptionen (engine.defaultOptions)
 * @param {string} langs     OCR-Sprachen für gescannte Seiten, z.B. "deu+eng"
 * @param {(pct:number)=>void} onProgress
 */
export async function redactPdf(file, options, langs = 'deu+eng', onProgress = null) {
    const pdfjs = await loadPdfjs();
    const data = new Uint8Array(await file.arrayBuffer());
    const doc = await pdfjs.getDocument({ data, isEvalSupported: false }).promise;

    const scale = 2;                 // gute Auflösung für die Schwärzung
    const pages = [];
    const mappings = [];
    let sensitiveCount = 0;

    for (let i = 1; i <= doc.numPages; i++) {
        const page = await doc.getPage(i);
        const { canvas, viewport } = await renderPageToCanvas(page, scale);

        // Bevorzugt die eingebettete Textebene nutzen (schnell, genau); nur wenn
        // eine Seite keinen Text hat (Scan), auf OCR ausweichen.
        let words = [];
        try {
            const tc = await page.getTextContent();
            words = pdfItemsToWords(tc.items, viewport.transform, viewport.scale);
        } catch {
            words = [];
        }
        if (words.length === 0) {
            words = await ocrWords(canvas, langs);
        }

        const plan = planImageRedaction(words, options, null);
        const redacted = drawRedacted(canvas, plan.boxes, 2);
        const jpeg = await canvasToJpeg(redacted);
        pages.push({ jpeg, width: redacted.width, height: redacted.height });

        for (const m of plan.mappings) {
            mappings.push(m);
            if (m.category === 'sensitive') {
                sensitiveCount++;
            }
        }
        onProgress?.(Math.round((i / doc.numPages) * 100));
    }

    const pdfBytes = imagesToPdf(pages);
    return {
        blob: new Blob([pdfBytes], { type: 'application/pdf' }),
        mappings,
        pageCount: doc.numPages,
        sensitiveCount
    };
}
