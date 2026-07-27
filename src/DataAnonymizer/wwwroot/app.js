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
    },
    paste: async function () {
        try {
            if (navigator.clipboard && navigator.clipboard.readText) {
                return await navigator.clipboard.readText();
            }
        } catch (e) {
            // Zugriff verweigert oder nicht verfügbar – leeren String liefern.
        }
        return "";
    }
};

window.appStorage = {
    get: function (key) {
        return window.localStorage.getItem(key);
    },
    set: function (key, value) {
        window.localStorage.setItem(key, value);
    },
    remove: function (key) {
        window.localStorage.removeItem(key);
    }
};

window.appFile = {
    download: function (filename, text) {
        const blob = new Blob([text], { type: "application/json" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
};
