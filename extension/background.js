// Service Worker der Erweiterung: fügt ein Rechtsklick-Menü hinzu, um markierten
// Text direkt auf jeder Webseite zu anonymisieren oder zurückzuübersetzen. Das
// Ergebnis landet in der Zwischenablage. Alles bleibt lokal – für das
// Kontextmenü wird nur die Muster-Erkennung genutzt (schnell, ohne LLM).

import { anonymize, deanonymize, defaultOptions } from './engine.js';

const MENU_ANON = 'anonymizer-anonymize';
const MENU_DEANON = 'anonymizer-deanonymize';

// Menüs bei Installation UND bei jedem Browserstart neu aufbauen. removeAll()
// verhindert den Fehler "duplicate id" beim Aktualisieren der Erweiterung.
function createMenus() {
    chrome.contextMenus.removeAll(() => {
        chrome.contextMenus.create({
            id: MENU_ANON,
            title: chrome.i18n.getMessage('ctxAnonymize') || 'Anonymize selection (copy)',
            contexts: ['selection']
        });
        chrome.contextMenus.create({
            id: MENU_DEANON,
            title: chrome.i18n.getMessage('ctxDeanonymize') || 'Restore selection (copy)',
            contexts: ['selection']
        });
    });
}

chrome.runtime.onInstalled.addListener(createMenus);
chrome.runtime.onStartup.addListener(createMenus);

chrome.contextMenus.onClicked.addListener(async (info) => {
    const text = info.selectionText ?? '';
    if (!text.trim()) {
        return;
    }
    if (info.menuItemId === MENU_ANON) {
        await handleAnonymize(text);
    } else if (info.menuItemId === MENU_DEANON) {
        await handleDeanonymize(text);
    }
});

async function handleAnonymize(text) {
    const stored = await chrome.storage.local.get(['options', 'customTerms', 'allowedTerms', 'lang']);
    const options = {
        ...defaultOptions(),
        ...(stored.options ?? {}),
        language: stored.lang ?? 'en',
        customTerms: splitLines(stored.customTerms),
        allowedTerms: splitLines(stored.allowedTerms)
    };

    const result = anonymize(text, options);
    // Zuordnung speichern, damit das Popup und "Zurückübersetzen" sie weiterverwenden.
    await chrome.storage.local.set({ mappings: result.mappings });

    await copyToClipboard(result.anonymizedText);
    const sensitive = result.mappings.filter(m => m.category === 'sensitive').length;
    notify(
        chrome.i18n.getMessage('notifyAnonymized') || 'Anonymized & copied',
        `${result.mappings.length} value(s) replaced, copied to clipboard.` +
        (sensitive > 0 ? ` ⚠️ ${sensitive} sensitive (revDSG).` : '')
    );
}

async function handleDeanonymize(text) {
    const stored = await chrome.storage.local.get(['mappings']);
    const mappings = Array.isArray(stored.mappings) ? stored.mappings : [];
    if (mappings.length === 0) {
        notify(
            chrome.i18n.getMessage('notifyRestored') || 'Restore',
            chrome.i18n.getMessage('notifyNoMapping') || 'No saved mapping yet - anonymize something first.'
        );
        return;
    }
    const restored = deanonymize(text, mappings);
    await copyToClipboard(restored);
    notify(
        chrome.i18n.getMessage('notifyRestored') || 'Restored & copied',
        'Original values put back, copied to clipboard.'
    );
}

function splitLines(value) {
    return (value ?? '').split('\n').map(s => s.trim()).filter(Boolean);
}

function notify(title, message) {
    chrome.notifications.create({
        type: 'basic',
        iconUrl: 'icons/icon128.png',
        title,
        message
    });
}

// ---- Zwischenablage über ein Offscreen-Dokument (MV3-Muster) ---------------

async function copyToClipboard(text) {
    await ensureOffscreen();
    await chrome.runtime.sendMessage({ target: 'offscreen', type: 'copy', text });
}

let creatingOffscreen = null;

async function ensureOffscreen() {
    try {
        if (await chrome.offscreen.hasDocument?.()) {
            return;
        }
    } catch {
        // hasDocument nicht verfügbar → einfach versuchen zu erstellen.
    }
    if (creatingOffscreen) {
        await creatingOffscreen;
        return;
    }
    try {
        creatingOffscreen = chrome.offscreen.createDocument({
            url: 'offscreen.html',
            reasons: ['CLIPBOARD'],
            justification: 'Write the anonymized/restored text to the clipboard.'
        });
        await creatingOffscreen;
    } catch {
        // Existiert bereits (Race) – dann ist alles gut.
    } finally {
        creatingOffscreen = null;
    }
}
