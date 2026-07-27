#!/usr/bin/env bash
# Startet den Daten-Anonymisierer lokal und öffnet den Browser.
set -e
cd "$(dirname "$0")"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "Fehler: .NET 8 SDK nicht gefunden."
    echo "Download: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

# Ollama automatisch starten, falls installiert, aber noch nicht aktiv (KI-Erkennung).
if command -v ollama >/dev/null 2>&1 && ! curl -s --max-time 2 http://localhost:11434/api/tags >/dev/null 2>&1; then
    echo "Starte Ollama im Hintergrund ..."
    (ollama serve >/dev/null 2>&1 &)
fi

URL="http://localhost:5100"
(
    sleep 3
    xdg-open "$URL" 2>/dev/null || open "$URL" 2>/dev/null || echo "Bitte im Browser öffnen: $URL"
) &

echo "Starte Daten-Anonymisierer auf $URL (Beenden mit Ctrl+C) ..."
dotnet run --project src/DataAnonymizer --urls "$URL"
