// Unsichtbares Offscreen-Dokument, das Text in die Zwischenablage schreibt.
// In MV3 kann der Service Worker die Clipboard-API nicht direkt nutzen; dieses
// Dokument übernimmt das über ein Textfeld + execCommand('copy').

chrome.runtime.onMessage.addListener((message) => {
    if (message?.target !== 'offscreen' || message.type !== 'copy') {
        return;
    }
    const ta = document.getElementById('clip');
    ta.value = message.text ?? '';
    ta.select();
    try {
        document.execCommand('copy');
    } catch {
        // Zwischenablage nicht verfügbar – dann bleibt der Text ungelegt.
    }
    ta.value = '';
});
