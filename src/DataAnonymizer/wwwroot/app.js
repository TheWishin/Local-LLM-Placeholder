// Kleine Hilfsfunktionen für Zwischenablage und lokale Speicherung.
// Alles bleibt im Browser des Benutzers – keine Übertragung an Dritte.
window.appClipboard = {
    copy: function (text) {
        if (navigator.clipboard && window.isSecureContext) {
            return navigator.clipboard.writeText(text);
        }
        // Fallback für http://localhost ohne Clipboard-API-Berechtigung
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.style.position = "fixed";
        ta.style.opacity = "0";
        document.body.appendChild(ta);
        ta.select();
        document.execCommand("copy");
        document.body.removeChild(ta);
    }
};

window.appStorage = {
    get: function (key) {
        return window.localStorage.getItem(key);
    },
    set: function (key, value) {
        window.localStorage.setItem(key, value);
    }
};
