// Popup-Logik: verbindet engine.js (Erkennung), ollama.js (lokales LLM) und
// i18n.js (Sprachen). Einstellungen liegen in chrome.storage.local, der letzte
// Arbeitsstand (inkl. Zuordnungstabelle) nur in chrome.storage.session –
// er verschwindet also, sobald der Browser geschlossen wird.

import { anonymize, deanonymize, labelFor, defaultOptions } from './engine.js';
import * as ollama from './ollama.js';
import { LANGUAGES, STRINGS, detectLanguage, format } from './i18n.js';

const $ = id => document.getElementById(id);

const state = {
    lang: 'en',
    options: defaultOptions(),
    customTermsText: '',
    llmEnabled: true,
    llmStatus: 'checking',   // checking | ready | offline | pulling | pullfailed
    llmModels: [],
    llmModel: '',
    pullPercent: 0,
    mappings: [],
    lastLlmFindings: null,
    allowedTermsText: '',
    busy: false
};

const t = () => STRINGS[state.lang];

// ---- Persistenz -----------------------------------------------------------

async function loadStored() {
    const local = await chrome.storage.local.get(['lang', 'options', 'customTerms', 'allowedTerms', 'llmEnabled', 'llmModel', 'mappings']);
    state.lang = local.lang ?? detectLanguage(navigator.language);
    state.options = { ...defaultOptions(), ...(local.options ?? {}) };
    state.customTermsText = local.customTerms ?? '';
    state.allowedTermsText = local.allowedTerms ?? '';
    state.llmEnabled = local.llmEnabled !== false; // Standard: an
    state.llmModel = local.llmModel ?? '';

    // Die Zuordnung überlebt Browser-Neustarts, damit Antworten und Skripte
    // auch später noch zurückübersetzt werden können.
    state.mappings = Array.isArray(local.mappings) ? local.mappings : [];

    const session = await chrome.storage.session.get(['input', 'output', 'response', 'restored', 'llmFindings']);
    $('inputText').value = session.input ?? '';
    state.lastLlmFindings = session.llmFindings ?? null;
    if (session.output !== undefined) {
        $('outputText').value = session.output;
        showResult(session.output, state.mappings);
    } else if (state.mappings.length > 0) {
        // Gespeicherte Zuordnung ohne aktuelles Ergebnis → De-Anonymisieren freischalten.
        $('deanonSection').classList.remove('hidden');
        $('mappingLoadedNote').classList.remove('hidden');
    }
    if (session.response) {
        $('responseText').value = session.response;
    }
    if (session.restored) {
        $('restoredText').value = session.restored;
        $('restoredText').classList.remove('hidden');
        $('copyRestoredBtn').classList.remove('hidden');
    }
}

const saveLocal = () => chrome.storage.local.set({
    lang: state.lang,
    options: state.options,
    customTerms: state.customTermsText,
    allowedTerms: state.allowedTermsText,
    llmEnabled: state.llmEnabled,
    llmModel: state.llmModel
});

const saveSession = () => chrome.storage.session.set({
    input: $('inputText').value,
    output: $('outputText').value,
    response: $('responseText').value,
    restored: $('restoredText').value,
    llmFindings: state.lastLlmFindings
});

const saveMappings = () => chrome.storage.local.set({ mappings: state.mappings });

// ---- Oberfläche -----------------------------------------------------------

const OPTION_KEYS = [
    ['names', 'optNames'], ['emails', 'optEmails'], ['phones', 'optPhones'],
    ['streets', 'optStreets'], ['cities', 'optCities'], ['iban', 'optIban'],
    ['ssn', 'optSsn'], ['cards', 'optCards'], ['refs', 'optRefs'],
    ['plates', 'optPlates'], ['orgs', 'optOrgs'],
    ['ip', 'optIp'], ['sensitive', 'optSensitive'],
    ['birthdays', 'optBirthdays'], ['allDates', 'optAllDates']
];

function renderTexts() {
    const s = t();
    $('appTitle').textContent = s.appTitle;
    $('tagline').textContent = s.tagline;
    $('privacyBadge').textContent = s.privacyBadge;
    $('stepAnon').textContent = s.stepAnon;
    $('stepUse').textContent = s.stepUse;
    $('stepRestore').textContent = s.stepRestore;
    $('inputLabel').textContent = s.inputLabel;
    $('inputText').placeholder = s.inputPlaceholder;
    $('anonymizeBtn').textContent = state.busy ? s.btnAnonymizeBusy : s.btnAnonymize;
    $('clearBtn').textContent = s.btnClear;
    $('pasteBtn').textContent = s.btnPaste;
    $('outputLabel').textContent = s.outputLabel;
    $('copyOutputBtn').textContent = s.btnCopy;
    $('mappingNote').textContent = s.mappingNote;
    $('deanonTitle').textContent = s.deanonTitle;
    $('responseText').placeholder = s.deanonPlaceholder;
    $('deanonymizeBtn').textContent = s.btnDeanonymize;
    $('copyRestoredBtn').textContent = s.btnCopy;
    $('settingsTitle').textContent = s.settingsTitle;
    $('termsLabel').textContent = s.termsLabel;
    $('customTerms').placeholder = s.termsPlaceholder;
    $('allowedLabel').textContent = s.allowedLabel;
    $('exportMappingBtn').textContent = s.btnExportMapping;
    $('importMappingBtn').textContent = s.btnImportMapping;
    $('deleteMappingBtn').textContent = s.btnDeleteMapping;
    $('mappingLoadedNote').textContent = format(s.mappingLoaded, state.mappings.length);
    $('llmEnableLabel').textContent = s.llmEnable;
    $('llmRecheck').textContent = s.btnRecheck;
    $('llmErrorNote').textContent = s.llmError;
    $('footerNote').textContent = s.footer;
    $('dsgNote').textContent = s.dsgNote;

    const grid = $('optionsGrid');
    grid.innerHTML = '';
    for (const [key, labelKey] of OPTION_KEYS) {
        const label = document.createElement('label');
        const box = document.createElement('input');
        box.type = 'checkbox';
        box.checked = state.options[key];
        box.addEventListener('change', () => {
            state.options[key] = box.checked;
            saveLocal();
        });
        label.append(box, document.createTextNode(labelKey in s ? s[labelKey] : key));
        grid.append(label);
    }

    if (state.mappings.length > 0 || $('outputText').value) {
        renderMappingTable();
    }
    renderLlm();
}

function renderLlm() {
    const s = t();
    $('llmEnabled').checked = state.llmEnabled;
    const status = $('llmStatus');
    const modelRow = $('llmModelRow');
    const progress = $('llmProgressWrap');
    const recheck = $('llmRecheck');
    modelRow.classList.add('hidden');
    progress.classList.add('hidden');
    recheck.classList.add('hidden');

    if (!state.llmEnabled) {
        status.textContent = '';
        return;
    }
    switch (state.llmStatus) {
        case 'checking':
            status.textContent = s.llmChecking;
            break;
        case 'ready': {
            status.textContent = s.llmReady;
            modelRow.classList.remove('hidden');
            const select = $('llmModelSelect');
            select.innerHTML = '';
            for (const m of state.llmModels) {
                const option = document.createElement('option');
                option.value = m;
                option.textContent = m;
                option.selected = m === state.llmModel;
                select.append(option);
            }
            break;
        }
        case 'pulling':
            status.textContent = format(s.llmPulling, ollama.DEFAULT_MODEL, state.pullPercent);
            progress.classList.remove('hidden');
            $('llmProgressBar').style.width = `${state.pullPercent}%`;
            break;
        case 'pullfailed':
            status.textContent = format(s.llmPullFailed, ollama.DEFAULT_MODEL);
            modelRow.classList.remove('hidden');
            $('llmModelSelect').innerHTML = '';
            recheck.classList.remove('hidden');
            break;
        case 'offline':
            status.textContent = s.llmOffline;
            modelRow.classList.remove('hidden');
            $('llmModelSelect').innerHTML = '';
            recheck.classList.remove('hidden');
            break;
    }
}

function renderMappingTable() {
    const s = t();
    $('outputSection').classList.remove('hidden');
    $('deanonSection').classList.remove('hidden');
    $('mappingSummary').textContent = state.mappings.length > 0
        ? format(s.mappingTitle, state.mappings.length)
        : s.noPiiFound;

    const table = $('mappingTable');
    table.innerHTML = '';
    for (const m of state.mappings) {
        const row = table.insertRow();
        const code = document.createElement('code');
        code.textContent = m.placeholder;
        row.insertCell().append(code);
        row.insertCell().textContent = m.original;
        const badge = document.createElement('span');
        badge.className = `cat-badge cat-${m.category}`;
        badge.textContent = labelFor(m.category, state.lang);
        row.insertCell().append(badge);
        const eye = document.createElement('button');
        eye.className = 'eye-btn';
        eye.textContent = '\u{1F441}';
        eye.title = s.allowTooltip;
        eye.addEventListener('click', () => allowValue(m.original));
        row.insertCell().append(eye);
    }
    renderChips();
    renderCatSummary();
    renderSensitiveWarning();
}

function renderSensitiveWarning() {
    const el = $('sensitiveWarning');
    const n = state.mappings.filter(m => m.category === 'sensitive').length;
    if (n > 0) {
        el.textContent = format(t().sensitiveWarning, n);
        el.classList.remove('hidden');
    } else {
        el.classList.add('hidden');
    }
}

function renderCatSummary() {
    const wrap = $('catSummary');
    wrap.innerHTML = '';
    if (state.mappings.length === 0) {
        return;
    }
    const counts = new Map();
    for (const m of state.mappings) {
        counts.set(m.category, (counts.get(m.category) ?? 0) + 1);
    }
    const summary = document.createElement('div');
    summary.className = 'muted small';
    summary.textContent = format(t().summaryLine, state.mappings.length, counts.size);
    wrap.append(summary);
    const row = document.createElement('div');
    row.className = 'cat-badges';
    for (const [cat, n] of [...counts.entries()].sort((a, b) => b[1] - a[1])) {
        const b = document.createElement('span');
        b.className = `cat-badge cat-${cat}`;
        b.textContent = `${labelFor(cat, state.lang)} \u00b7 ${n}`;
        row.append(b);
    }
    wrap.append(row);
}

function renderChips() {
    const s = t();
    const wrap = $('allowedChips');
    const terms = allowedTermsList();
    wrap.innerHTML = '';
    if (terms.length === 0) {
        wrap.classList.add('hidden');
        return;
    }
    wrap.classList.remove('hidden');
    const title = document.createElement('span');
    title.className = 'muted small';
    title.textContent = s.allowedChipsTitle;
    wrap.append(title);
    for (const term of terms) {
        const chip = document.createElement('button');
        chip.className = 'chip';
        chip.textContent = `${term} \u2715`;
        chip.title = s.allowedChipRemoveTooltip;
        chip.addEventListener('click', () => removeAllowed(term));
        wrap.append(chip);
    }
}

// ---- Erlaubte Werte -------------------------------------------------------

function allowedTermsList() {
    const seen = new Set();
    const result = [];
    for (const line of state.allowedTermsText.split('\n')) {
        const term = line.trim();
        if (term && !seen.has(term.toLowerCase())) {
            seen.add(term.toLowerCase());
            result.push(term);
        }
    }
    return result;
}

// Neu anonymisieren ohne erneuten LLM-Aufruf (z.B. nach Freigabe eines Werts).
function reAnonymize() {
    const options = {
        ...state.options,
        language: state.lang,
        customTerms: state.customTermsText.split('\n').map(x => x.trim()).filter(Boolean),
        allowedTerms: allowedTermsList()
    };
    const result = anonymize($('inputText').value, options, state.lastLlmFindings);
    showResult(result.anonymizedText, result.mappings);
    saveSession();
}

function allowValue(original) {
    const terms = allowedTermsList();
    if (!terms.some(t2 => t2.toLowerCase() === original.toLowerCase())) {
        terms.push(original);
    }
    state.allowedTermsText = terms.join('\n');
    $('allowedTerms').value = state.allowedTermsText;
    saveLocal();
    reAnonymize();
}

function removeAllowed(term) {
    state.allowedTermsText = allowedTermsList().filter(t2 => t2.toLowerCase() !== term.toLowerCase()).join('\n');
    $('allowedTerms').value = state.allowedTermsText;
    saveLocal();
    reAnonymize();
}

function showResult(output, mappings) {
    state.mappings = mappings;
    $('outputText').value = output;
    $('mappingLoadedNote').classList.add('hidden');
    renderMappingTable();
    saveMappings();
}

// ---- Lokales LLM ----------------------------------------------------------

// Sorgt ohne Zutun des Benutzers dafür, dass die KI-Erkennung bereit wird:
// Ollama suchen, Modell wählen, notfalls das Standardmodell herunterladen.
async function ensureLlm({ allowPull = true } = {}) {
    state.llmStatus = 'checking';
    renderLlm();

    const { reachable, models } = await ollama.getState();
    if (!reachable) {
        state.llmStatus = 'offline';
        renderLlm();
        return;
    }

    // Embedding-Modelle können nicht chatten.
    const chatModels = models.filter(m => !m.toLowerCase().includes('embed'));
    state.llmModels = chatModels;
    const pick = chatModels.find(m => m === state.llmModel)
        ?? chatModels.find(m => m.toLowerCase().startsWith(ollama.DEFAULT_MODEL))
        ?? chatModels[0];
    if (pick) {
        state.llmModel = pick;
        state.llmStatus = 'ready';
        renderLlm();
        saveLocal();
        ollama.warmUp(pick);
        return;
    }

    if (!allowPull) {
        // Ollama läuft, aber kein Modell installiert. Im Popup NICHT herunterladen
        // (der Download würde beim Schliessen des Popups abbrechen) – stattdessen
        // den manuellen Hinweis zeigen. Die Desktop-App lädt das Modell zuverlässig.
        state.llmStatus = 'pullfailed';
        renderLlm();
        return;
    }

    // Ollama läuft, aber kein Modell installiert → automatisch holen.
    state.llmStatus = 'pulling';
    state.pullPercent = 0;
    renderLlm();
    let ok = false;
    try {
        ok = await ollama.pullModel(ollama.DEFAULT_MODEL, pc => {
            if (pc !== state.pullPercent) {
                state.pullPercent = pc;
                renderLlm();
            }
        });
    } catch {
        ok = false;
    }
    if (!ok) {
        state.llmStatus = 'pullfailed';
        renderLlm();
        return;
    }
    await ensureLlm({ allowPull: false });
}

// ---- Aktionen -------------------------------------------------------------

async function runAnonymize() {
    state.busy = true;
    $('anonymizeBtn').disabled = true;
    $('anonymizeBtn').textContent = t().btnAnonymizeBusy;
    $('llmErrorNote').classList.add('hidden');

    const text = $('inputText').value;
    const options = {
        ...state.options,
        language: state.lang,
        customTerms: state.customTermsText.split('\n').map(x => x.trim()).filter(Boolean),
        allowedTerms: allowedTermsList()
    };

    // Falls Ollama inzwischen gestartet wurde, ohne Neuöffnen erkennen.
    // Kein Modell-Download hier – Anonymisieren soll immer sofort laufen.
    if (state.llmEnabled && state.llmStatus === 'offline') {
        await ensureLlm({ allowPull: false });
    }

    let llmFindings = null;
    if (state.llmEnabled && state.llmStatus === 'ready' && state.llmModel && text.trim()) {
        try {
            llmFindings = await ollama.detectPii(text, state.llmModel);
        } catch {
            $('llmErrorNote').classList.remove('hidden');
            state.llmStatus = 'offline';
            renderLlm();
        }
    }

    state.lastLlmFindings = llmFindings;
    const result = anonymize(text, options, llmFindings);
    showResult(result.anonymizedText, result.mappings);
    $('restoredText').classList.add('hidden');
    $('copyRestoredBtn').classList.add('hidden');
    $('restoredText').value = '';

    state.busy = false;
    $('anonymizeBtn').disabled = false;
    $('anonymizeBtn').textContent = t().btnAnonymize;
    saveSession();
}

function runDeanonymize() {
    const restored = deanonymize($('responseText').value, state.mappings);
    $('restoredText').value = restored;
    $('restoredText').classList.remove('hidden');
    $('copyRestoredBtn').classList.remove('hidden');
    saveSession();
}

function clearAll() {
    $('inputText').value = '';
    $('outputText').value = '';
    $('responseText').value = '';
    $('restoredText').value = '';
    state.lastLlmFindings = null;
    $('outputSection').classList.add('hidden');
    $('llmErrorNote').classList.add('hidden');
    chrome.storage.session.remove(['input', 'output', 'response', 'restored', 'llmFindings']);
    // Die gespeicherte Zuordnung bleibt erhalten (löschen über 🗑),
    // damit Antworten weiterhin zurückübersetzt werden können.
    if (state.mappings.length > 0) {
        $('mappingLoadedNote').classList.remove('hidden');
        $('mappingLoadedNote').textContent = format(t().mappingLoaded, state.mappings.length);
    } else {
        $('deanonSection').classList.add('hidden');
    }
}

async function copyButton(button, textareaId) {
    await navigator.clipboard.writeText($(textareaId).value);
    const original = t().btnCopy;
    button.textContent = t().copied;
    setTimeout(() => { button.textContent = original; }, 1500);
}

// ---- Start ----------------------------------------------------------------

async function main() {
    await loadStored();

    const languageSelect = $('languageSelect');
    for (const { code, native } of LANGUAGES) {
        const option = document.createElement('option');
        option.value = code;
        option.textContent = native;
        languageSelect.append(option);
    }
    languageSelect.value = state.lang;
    languageSelect.addEventListener('change', () => {
        state.lang = languageSelect.value;
        saveLocal();
        renderTexts();
    });

    $('customTerms').value = state.customTermsText;
    $('customTerms').addEventListener('input', () => {
        state.customTermsText = $('customTerms').value;
        saveLocal();
    });

    $('allowedTerms').value = state.allowedTermsText;
    $('allowedTerms').addEventListener('input', () => {
        state.allowedTermsText = $('allowedTerms').value;
        saveLocal();
        if (!$('outputSection').classList.contains('hidden')) {
            reAnonymize();
        }
    });

    $('llmEnabled').addEventListener('change', () => {
        state.llmEnabled = $('llmEnabled').checked;
        saveLocal();
        if (state.llmEnabled) {
            ensureLlm({ allowPull: false });
        } else {
            renderLlm();
        }
    });
    $('llmModelSelect').addEventListener('change', () => {
        state.llmModel = $('llmModelSelect').value;
        saveLocal();
        ollama.warmUp(state.llmModel);
    });
    $('llmRecheck').addEventListener('click', () => ensureLlm({ allowPull: false }));

    $('exportMappingBtn').addEventListener('click', () => {
        if (state.mappings.length === 0) {
            return;
        }
        const blob = new Blob([JSON.stringify(state.mappings, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        const stamp = new Date().toISOString().slice(0, 16).replace(/[:T]/g, '-');
        a.download = `anonymizer-mapping-${stamp}.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    });
    $('importMappingBtn').addEventListener('click', () => $('mappingFile').click());
    $('mappingFile').addEventListener('change', async () => {
        const file = $('mappingFile').files?.[0];
        if (!file) {
            return;
        }
        try {
            const data = JSON.parse(await file.text());
            const imported = Array.isArray(data)
                ? data.filter(m => typeof m?.placeholder === 'string' && typeof m?.original === 'string')
                    .map(m => ({ placeholder: m.placeholder, original: m.original, category: m.category ?? 'term' }))
                : [];
            if (imported.length > 0) {
                state.mappings = imported;
                saveMappings();
                $('deanonSection').classList.remove('hidden');
                renderTexts();
                renderMappingTable();
            }
        } catch {
            // Keine gültige Zuordnungsdatei – nichts ändern.
        }
        $('mappingFile').value = '';
    });
    $('deleteMappingBtn').addEventListener('click', () => {
        state.mappings = [];
        chrome.storage.local.remove('mappings');
        $('restoredText').value = '';
        $('restoredText').classList.add('hidden');
        $('copyRestoredBtn').classList.add('hidden');
        $('outputSection').classList.add('hidden');
        $('deanonSection').classList.add('hidden');
        $('outputText').value = '';
        saveSession();
    });

    $('anonymizeBtn').addEventListener('click', runAnonymize);
    $('clearBtn').addEventListener('click', clearAll);
    $('pasteBtn').addEventListener('click', async () => {
        try {
            const text = await navigator.clipboard.readText();
            if (text) {
                $('inputText').value = text;
                saveSession();
            }
        } catch {
            // Clipboard-Zugriff verweigert - nichts tun.
        }
    });
    $('deanonymizeBtn').addEventListener('click', runDeanonymize);
    $('copyOutputBtn').addEventListener('click', e => copyButton(e.target, 'outputText'));
    $('copyRestoredBtn').addEventListener('click', e => copyButton(e.target, 'restoredText'));
    $('inputText').addEventListener('input', saveSession);
    $('responseText').addEventListener('input', saveSession);

    renderTexts();
    if (state.llmEnabled) {
        ensureLlm({ allowPull: false });
    }
}

main();
