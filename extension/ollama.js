// Client für das lokale LLM (Ollama, http://localhost:11434).
// JavaScript-Gegenstück zu src/DataAnonymizer.Core/LocalLlmClient.cs –
// bei Änderungen beide Seiten synchron halten. Kein Zeichen verlässt den Rechner.

export const DEFAULT_ENDPOINT = 'http://localhost:11434';
export const DEFAULT_MODEL = 'llama3.2';

// Der System-Prompt bringt dem Modell bei, was als "persönliche Daten" gilt –
// unabhängig von der Sprache des Eingabetexts.
const SYSTEM_PROMPT = `You are a privacy assistant. Find all personally identifiable information (PII)
in the user's text. The text can be in any language (German, English, French,
Italian, or others) - you understand them all.

Respond ONLY with a JSON object in exactly this form:
{"entities":[{"text":"<exact substring from the input>","category":"<category>"}]}

Allowed categories:
- "name": names of real people (with or without salutation)
- "organization": names of companies, employers, associations, institutions
- "email": e-mail addresses
- "phone": phone or fax numbers
- "address": street names with house numbers
- "location": postal codes with towns or cities that reveal where someone lives
- "id": customer, policy, case, insurance, social security or account numbers, IBANs, credit card numbers, license plates
- "ip": IP addresses (IPv4 or IPv6)
- "birthdate": dates of birth
- "sensitive": special-category personal data under the Swiss revised Data
  Protection Act (revDSG) and the EU GDPR: health / medical conditions,
  diagnoses, disabilities; religious or philosophical beliefs; political
  opinions; trade-union membership; racial or ethnic origin; genetic or
  biometric data; sex life or sexual orientation; and data about social
  assistance or administrative/criminal proceedings and sanctions. Report the
  specific revealing phrase (e.g. "Diagnose: Depression", "katholisch"), not
  the whole sentence.
- "other": anything else that could identify a specific person

Rules:
- Copy "text" verbatim from the input, character for character.
- List each distinct value once, even if it appears several times.
- Do NOT report: generic words, job titles alone, country names, amounts of
  money, dates that are not birth dates, or placeholders like [NAME_1].
- Pay special attention to "sensitive" data - under the revDSG it needs the
  strongest protection, so never miss health, religion or similar details.
- If there is no PII, respond with {"entities":[]}.`;

// Kategorien des Modells → Kategorien der Engine (engine.js).
const CATEGORY_MAP = {
    name: 'name', person: 'name',
    organization: 'org', organisation: 'org', company: 'org',
    email: 'email', 'e-mail': 'email',
    phone: 'phone', telephone: 'phone',
    address: 'address', street: 'address',
    location: 'city', city: 'city', place: 'city',
    iban: 'iban', bank: 'iban',
    id: 'ref', number: 'ref', reference: 'ref', account: 'ref',
    birthdate: 'date', date: 'date', dob: 'date',
    plate: 'plate', license_plate: 'plate',
    card: 'card', credit_card: 'card',
    ssn: 'ssn', social_security: 'ssn',
    ip: 'ip', ip_address: 'ip',
    sensitive: 'sensitive', health: 'sensitive', religion: 'sensitive',
    political: 'sensitive', union: 'sensitive', biometric: 'sensitive',
    genetic: 'sensitive', sexuality: 'sensitive', criminal: 'sensitive'
};

/** Erreichbarkeit prüfen und installierte Modelle auflisten (max. 3 Sekunden). */
export async function getState(endpoint = DEFAULT_ENDPOINT) {
    try {
        const response = await fetch(`${endpoint}/api/tags`, { signal: AbortSignal.timeout(3000) });
        if (!response.ok) {
            return { reachable: false, models: [] };
        }
        const data = await response.json();
        const models = Array.isArray(data.models)
            ? data.models.map(m => m?.name).filter(n => typeof n === 'string' && n.length > 0)
            : [];
        return { reachable: true, models };
    } catch {
        return { reachable: false, models: [] };
    }
}

/** Lässt das lokale Modell den Text nach persönlichen Daten durchsuchen. */
export async function detectPii(text, model, endpoint = DEFAULT_ENDPOINT) {
    if (!text || !text.trim()) {
        return [];
    }
    const response = await fetch(`${endpoint}/api/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            model,
            stream: false,
            format: 'json',
            // Modell nach der Analyse im Speicher halten – die nächste Anfrage
            // startet dann ohne Kaltstart und liefert schneller.
            keep_alive: '30m',
            options: { temperature: 0 },
            messages: [
                { role: 'system', content: SYSTEM_PROMPT },
                { role: 'user', content: text }
            ]
        })
    });
    if (!response.ok) {
        throw new Error(`Ollama antwortete mit HTTP ${response.status}`);
    }
    const data = await response.json();
    return parseEntities(data?.message?.content ?? '');
}

/**
 * Wandelt die JSON-Antwort des Modells in Entitäten { text, category } um.
 * Tolerant gegenüber Code-Fences oder Text um das JSON herum.
 */
export function parseEntities(answer) {
    if (!answer || !answer.trim()) {
        return [];
    }
    const start = answer.indexOf('{');
    const end = answer.lastIndexOf('}');
    if (start < 0 || end <= start) {
        return [];
    }
    let doc;
    try {
        doc = JSON.parse(answer.slice(start, end + 1));
    } catch {
        return [];
    }
    if (!Array.isArray(doc?.entities)) {
        return [];
    }

    const seen = new Set();
    const result = [];
    for (const item of doc.entities) {
        const value = typeof item?.text === 'string' ? item.text.trim() : '';
        if (!value || seen.has(value)) {
            continue;
        }
        seen.add(value);
        const key = typeof item?.category === 'string' ? item.category.trim().toLowerCase() : '';
        result.push({ text: value, category: CATEGORY_MAP[key] ?? 'term' });
    }
    return result;
}

/**
 * Lädt ein Modell über Ollama herunter (einmalig, mehrere GB). Meldet den
 * Fortschritt (0–100) und liefert true, wenn das Modell danach bereitsteht.
 * Wird der Download unterbrochen, setzt Ollama beim nächsten Versuch fort.
 */
export async function pullModel(model, onProgress, endpoint = DEFAULT_ENDPOINT) {
    const response = await fetch(`${endpoint}/api/pull`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        // "name" zusätzlich zu "model" für ältere Ollama-Versionen.
        body: JSON.stringify({ model, name: model, stream: true })
    });
    if (!response.ok || !response.body) {
        return false;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let success = false;
    for (;;) {
        const { done, value } = await reader.read();
        if (done) {
            break;
        }
        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() ?? '';
        for (const line of lines) {
            if (!line.trim()) {
                continue;
            }
            let p;
            try {
                p = JSON.parse(line);
            } catch {
                continue;
            }
            if (p.error) {
                return false;
            }
            if (typeof p.total === 'number' && typeof p.completed === 'number' && p.total > 0) {
                onProgress?.(Math.min(100, Math.max(0, Math.floor(p.completed * 100 / p.total))));
            }
            if (typeof p.status === 'string' && p.status.toLowerCase() === 'success') {
                success = true;
            }
        }
    }
    return success;
}

/** Modell in den Speicher laden, damit die erste Analyse nicht warten muss. */
export async function warmUp(model, endpoint = DEFAULT_ENDPOINT) {
    try {
        await fetch(`${endpoint}/api/generate`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ model, keep_alive: '15m' })
        });
    } catch {
        // Nur eine Beschleunigung – Fehler hier sind egal.
    }
}
