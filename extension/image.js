// Bild-Anonymisierung: erkennt per lokaler OCR (Tesseract.js), WO im Bild
// persönliche Daten stehen, und schwärzt diese Stellen. Alles läuft lokal im
// Browser – kein Upload, keine Cloud. Die OCR-Dateien liegen gebündelt unter
// vendor/tesseract/ (im Release enthalten).
//
// planImageRedaction() ist reine, testbare Logik (Node-Tests). Der OCR-Aufruf
// und das Zeichnen benötigen den Browser.

import { anonymize } from './engine.js';

/**
 * Stehen zwei Wörter auf derselben Zeile? Beurteilt über die senkrechte
 * Überlappung der Kästchen (robust gegen leicht unterschiedliche Höhen von
 * Gross-/Kleinbuchstaben). Reine Funktion.
 */
export function sameLine(a, b) {
    if (!a || !b) {
        return true;               // Ohne Position lieber wie bisher trennen (Leerzeichen).
    }
    const overlap = Math.min(a.y1, b.y1) - Math.max(a.y0, b.y0);
    const smaller = Math.max(1, Math.min(a.y1 - a.y0, b.y1 - b.y0));
    return overlap / smaller > 0.4;
}

/**
 * Bestimmt aus OCR-Wörtern (mit Position) die zu ersetzenden Kästchen.
 * Jedes Kästchen trägt den zugehörigen Platzhalter ([NAME_1] …), damit er
 * wahlweise ins Bild geschrieben (statt nur geschwärzt) werden kann.
 * Über options.existingMappings läuft die Nummerierung mit Text/anderen Seiten weiter.
 * @param {{text:string, bbox:{x0:number,y0:number,x1:number,y1:number}}[]} words
 * @param {object} options  Erkennungsoptionen (siehe engine.defaultOptions)
 * @param {{text:string,category:string}[]|null} llmFindings  optionale LLM-Funde
 * @returns {{boxes:{x0,y0,x1,y1,category,placeholder}[], mappings:object[], text:string, anonymizedText:string}}
 */
export function planImageRedaction(words, options, llmFindings = null) {
    // 1. Wörter zu Zeilen gruppieren. Die Erkennung läuft dann ZEILENWEISE –
    //    sonst würde ein Muster über das Zeilenende hinaus greifen (aus
    //    "Frau Anna Meier" + nächster Zeile "Kunde: ..." würde sonst
    //    fälschlich der Name "Anna Meier Kunde").
    const lines = [];
    words.forEach((w, i) => {
        const last = lines[lines.length - 1];
        if (last && sameLine(words[last[last.length - 1]].bbox, w.bbox)) {
            last.push(i);
        } else {
            lines.push([i]);
        }
    });

    const boxes = [];
    const mappings = [];
    const known = new Set();
    const textLines = [];
    const anonLines = [];

    for (const line of lines) {
        // 2. Text dieser Zeile zusammensetzen, Zeichen-Positionen je Wort merken.
        let text = '';
        const spans = [];
        line.forEach((wi, k) => {
            if (k > 0) {
                text += ' ';
            }
            const start = text.length;
            text += words[wi].text ?? '';
            spans.push({ start, end: text.length, index: wi });
        });

        // 3. Dieselbe Erkennung wie beim Text – mit fortlaufender Zuordnung,
        //    damit derselbe Wert über Zeilen (und Seiten) denselben Platzhalter hat.
        const result = anonymize(text, {
            ...options,
            existingMappings: [...(options.existingMappings ?? []), ...mappings]
        }, llmFindings);
        textLines.push(text);
        anonLines.push(result.anonymizedText);

        for (const m of result.mappings) {
            if (!known.has(m.placeholder)) {
                known.add(m.placeholder);
                mappings.push(m);
            }
        }

        // 4. Zeichen-Bereiche der gefundenen Werte (mit Platzhalter).
        const ranges = [];
        for (const m of result.mappings) {
            const needle = m.original;
            if (!needle) {
                continue;
            }
            let idx = 0;
            while ((idx = text.indexOf(needle, idx)) >= 0) {
                ranges.push({ start: idx, end: idx + needle.length, category: m.category, placeholder: m.placeholder });
                idx += needle.length;
            }
        }

        // 5. Jedes Wort, das einen Treffer überlappt, wird ersetzt. Zusammen-
        //    hängende Wörter desselben Treffers (Vor- + Nachname) ergeben EIN
        //    Kästchen, damit der Platzhalter einmal statt doppelt dasteht.
        let current = null;
        for (const sp of spans) {
            const hit = ranges.find(r => sp.start < r.end && r.start < sp.end);
            const bbox = words[sp.index].bbox;
            if (!hit || !bbox) {
                current = null;
                continue;
            }
            if (current && current._range === hit) {
                current.x0 = Math.min(current.x0, bbox.x0);
                current.y0 = Math.min(current.y0, bbox.y0);
                current.x1 = Math.max(current.x1, bbox.x1);
                current.y1 = Math.max(current.y1, bbox.y1);
                continue;
            }
            current = {
                x0: bbox.x0, y0: bbox.y0, x1: bbox.x1, y1: bbox.y1,
                category: hit.category, placeholder: hit.placeholder, _range: hit
            };
            boxes.push(current);
        }
    }

    for (const b of boxes) {
        delete b._range;
    }

    return {
        boxes,
        mappings,
        text: textLines.join('\n'),
        anonymizedText: anonLines.join('\n')
    };
}

/**
 * Berechnet Position und Schriftgrösse für einen ins Bild geschriebenen
 * Platzhalter. Reine Funktion – `measureAt(text, fontSize)` liefert die
 * Textbreite, damit sie ohne Browser testbar ist.
 * Passt die Schrift in die Höhe des Kästchens ein und verbreitert das
 * Kästchen nach rechts, falls der Platzhalter sonst nicht lesbar wäre.
 */
export function layoutPlaceholderBox(box, text, measureAt, canvasWidth, opts = {}) {
    const pad = opts.pad ?? 2;
    const minFont = opts.minFont ?? 9;
    const maxFont = opts.maxFont ?? 72;

    const x0 = Math.max(0, box.x0 - pad);
    const y0 = Math.max(0, box.y0 - pad);
    const y1 = box.y1 + pad;
    const height = Math.max(1, y1 - y0);

    let fontSize = Math.max(minFont, Math.min(maxFont, Math.floor(height * 0.78)));
    const available = Math.max(1, (box.x1 + pad) - x0);

    // Schrift verkleinern, bis der Platzhalter in die Breite passt.
    while (fontSize > minFont && measureAt(text, fontSize) > available) {
        fontSize--;
    }

    // Reicht die Breite immer noch nicht, das Kästchen nach rechts verbreitern
    // (bis zum Bildrand) – lieber etwas breiter als unlesbar.
    let x1 = box.x1 + pad;
    const needed = measureAt(text, fontSize) + pad * 2;
    if (needed > x1 - x0) {
        x1 = Math.min(canvasWidth, x0 + needed);
    }

    return { x0, y0, x1, y1, fontSize };
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
 * Zeichnet das Bild in ein Canvas und ersetzt die gefundenen Stellen.
 * mode = 'placeholder': die Stelle wird überdeckt und der Platzhalter
 *   ([NAME_1] …) gut lesbar hineingeschrieben – so bleibt der Rundlauf
 *   möglich (KI nutzt den Platzhalter, danach zurückübersetzen).
 * mode = 'blackout': die Stelle wird einfach geschwärzt (nicht umkehrbar).
 * @returns {HTMLCanvasElement}
 */
export function drawAnonymized(imageBitmap, boxes, opts = {}) {
    const mode = opts.mode ?? 'placeholder';
    const pad = opts.pad ?? 2;

    const canvas = document.createElement('canvas');
    canvas.width = imageBitmap.width;
    canvas.height = imageBitmap.height;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(imageBitmap, 0, 0);
    ctx.textBaseline = 'middle';

    const measureAt = (text, fontSize) => {
        ctx.font = `bold ${fontSize}px monospace`;
        return ctx.measureText(text).width;
    };

    for (const b of boxes) {
        if (mode === 'blackout' || !b.placeholder) {
            ctx.fillStyle = '#000';
            ctx.fillRect(Math.max(0, b.x0 - pad), Math.max(0, b.y0 - pad),
                (b.x1 - b.x0) + pad * 2, (b.y1 - b.y0) + pad * 2);
            continue;
        }

        const L = layoutPlaceholderBox(b, b.placeholder, measureAt, canvas.width, { pad });
        // Heller Kasten mit dunklem Rand + dunkler Schrift = maximaler Kontrast,
        // damit KI-Werkzeuge (und Menschen) den Platzhalter sicher lesen.
        ctx.fillStyle = '#FFFFFF';
        ctx.fillRect(L.x0, L.y0, L.x1 - L.x0, L.y1 - L.y0);
        ctx.strokeStyle = '#111111';
        ctx.lineWidth = 1;
        ctx.strokeRect(L.x0 + 0.5, L.y0 + 0.5, (L.x1 - L.x0) - 1, (L.y1 - L.y0) - 1);
        ctx.fillStyle = '#111111';
        ctx.font = `bold ${L.fontSize}px monospace`;
        ctx.fillText(b.placeholder, L.x0 + pad, (L.y0 + L.y1) / 2);
    }
    return canvas;
}

/** Rückwärtskompatibel: schwärzt die Kästchen (wie bisher). */
export function drawRedacted(imageBitmap, boxes, pad = 2) {
    return drawAnonymized(imageBitmap, boxes, { mode: 'blackout', pad });
}
