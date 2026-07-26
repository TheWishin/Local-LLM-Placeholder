// Bild-Anonymisierung: erkennt per lokaler OCR (Tesseract.js), WO im Bild
// persönliche Daten stehen, und schwärzt diese Stellen. Alles läuft lokal im
// Browser – kein Upload, keine Cloud. Die OCR-Dateien liegen gebündelt unter
// vendor/tesseract/ (im Release enthalten).
//
// planImageRedaction() ist reine, testbare Logik (Node-Tests). Der OCR-Aufruf
// und das Zeichnen benötigen den Browser.

import { anonymize } from './engine.js';

/**
 * Bestimmt aus OCR-Wörtern (mit Position) die zu schwärzenden Kästchen.
 * @param {{text:string, bbox:{x0:number,y0:number,x1:number,y1:number}}[]} words
 * @param {object} options  Erkennungsoptionen (siehe engine.defaultOptions)
 * @param {{text:string,category:string}[]|null} llmFindings  optionale LLM-Funde
 * @returns {{boxes:{x0,y0,x1,y1,category}[], mappings:object[], text:string}}
 */
export function planImageRedaction(words, options, llmFindings = null) {
    // 1. Text aus den Wörtern zusammensetzen und Zeichen-Positionen je Wort merken.
    let text = '';
    const spans = [];
    words.forEach((w, i) => {
        if (i > 0) {
            text += ' ';
        }
        const start = text.length;
        text += w.text ?? '';
        spans.push({ start, end: text.length, index: i });
    });

    // 2. Dieselbe Erkennung wie beim Text anwenden.
    const result = anonymize(text, options, llmFindings);

    // 3. Zeichen-Bereiche der gefundenen Werte im Text sammeln.
    const ranges = [];
    for (const m of result.mappings) {
        const needle = m.original;
        if (!needle) {
            continue;
        }
        let idx = 0;
        while ((idx = text.indexOf(needle, idx)) >= 0) {
            ranges.push({ start: idx, end: idx + needle.length, category: m.category });
            idx += needle.length;
        }
    }

    // 4. Jedes Wort, das einen Treffer-Bereich überlappt, wird geschwärzt.
    const boxes = [];
    for (const sp of spans) {
        const hit = ranges.find(r => sp.start < r.end && r.start < sp.end);
        if (hit && words[sp.index].bbox) {
            boxes.push({ ...words[sp.index].bbox, category: hit.category });
        }
    }

    return { boxes, mappings: result.mappings, text };
}

/** URL einer gebündelten Datei in der Erweiterung. */
function vendor(path) {
    return chrome.runtime.getURL(`vendor/tesseract/${path}`);
}

/** Prüft, ob die OCR-Dateien mitgeliefert wurden (im Release der Fall). */
export async function isOcrAvailable() {
    try {
        const res = await fetch(vendor('tesseract.min.js'), { method: 'HEAD' });
        return res.ok;
    } catch {
        return false;
    }
}

let workerPromise = null;

/** Lädt (einmalig) den lokalen Tesseract-Worker mit den gewünschten Sprachen. */
async function getWorker(langs) {
    if (workerPromise) {
        return workerPromise;
    }
    workerPromise = (async () => {
        // tesseract.min.js registriert global "Tesseract".
        await import(vendor('tesseract.min.js'));
        const Tesseract = globalThis.Tesseract;
        // Wichtig: die konkrete Core-Datei vorgeben. Sonst versucht tesseract.js,
        // per Feature-Erkennung eine Variante (z.B. "relaxedsimd") zu laden, die
        // im gebündelten Core evtl. nicht existiert. Die SIMD-LSTM-Variante läuft
        // in allen aktuellen Chrome/Edge-Versionen.
        const worker = await Tesseract.createWorker(langs, 1, {
            workerPath: vendor('worker.min.js'),
            corePath: vendor('tesseract-core-simd-lstm.wasm.js'),
            langPath: vendor('lang'),             // Verzeichnis mit den *.traineddata.gz
            workerBlobURL: false,
            gzip: true
        });
        return worker;
    })();
    return workerPromise;
}

/**
 * Führt OCR auf einem Bild aus und liefert erkannte Wörter mit Position.
 * @param {Blob|HTMLImageElement|HTMLCanvasElement|string} image
 * @param {string} langs z.B. "deu+eng"
 */
export async function ocrWords(image, langs = 'deu+eng', onProgress = null) {
    const worker = await getWorker(langs);
    if (onProgress) {
        // tesseract meldet Fortschritt über den Logger beim createWorker; hier
        // einfach Start/Ende signalisieren.
        onProgress(10);
    }
    // Wichtig: TSV-Ausgabe explizit anfordern – tesseract.js v6+ berechnet sie
    // sonst nicht, und die Wortpositionen fehlen.
    const { data } = await worker.recognize(image, {}, { text: true, tsv: true });
    if (onProgress) {
        onProgress(100);
    }
    // tesseract.js liefert je nach Version data.words nicht direkt; die
    // Wortpositionen stehen zuverlässig im TSV-Output.
    if (Array.isArray(data.words) && data.words.length > 0) {
        return data.words
            .filter(w => w.text && w.text.trim())
            .map(w => ({ text: w.text, bbox: { x0: w.bbox.x0, y0: w.bbox.y0, x1: w.bbox.x1, y1: w.bbox.y1 } }));
    }
    return parseTsvWords(data.tsv ?? '');
}

/**
 * Liest Wörter samt Position aus dem TSV-Output von Tesseract.
 * Spalten: level, page, block, par, line, word, left, top, width, height, conf, text.
 * level 5 = Wort. Reine, testbare Funktion.
 */
export function parseTsvWords(tsv, minConfidence = 30) {
    const words = [];
    for (const line of (tsv ?? '').split('\n')) {
        const c = line.split('\t');
        if (c.length < 12 || c[0] !== '5') {
            continue;
        }
        const left = Number(c[6]);
        const top = Number(c[7]);
        const width = Number(c[8]);
        const height = Number(c[9]);
        const conf = Number(c[10]);
        const text = c.slice(11).join('\t').trim();
        if (!text || Number.isNaN(left) || conf < minConfidence) {
            continue;
        }
        words.push({ text, bbox: { x0: left, y0: top, x1: left + width, y1: top + height } });
    }
    return words;
}

/**
 * Zeichnet das Bild in ein Canvas und schwärzt die angegebenen Kästchen.
 * @returns {HTMLCanvasElement}
 */
export function drawRedacted(imageBitmap, boxes, pad = 2) {
    const canvas = document.createElement('canvas');
    canvas.width = imageBitmap.width;
    canvas.height = imageBitmap.height;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(imageBitmap, 0, 0);
    ctx.fillStyle = '#000';
    for (const b of boxes) {
        const x = Math.max(0, b.x0 - pad);
        const y = Math.max(0, b.y0 - pad);
        const w = (b.x1 - b.x0) + pad * 2;
        const h = (b.y1 - b.y0) + pad * 2;
        ctx.fillRect(x, y, w, h);
    }
    return canvas;
}
