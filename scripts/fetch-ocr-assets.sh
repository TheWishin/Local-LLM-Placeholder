#!/usr/bin/env bash
# Lädt die lokalen OCR-Bausteine (Tesseract.js + Sprachdaten) und legt sie unter
# extension/vendor/tesseract/ ab. Diese Dateien sind gross (~30 MB) und deshalb
# NICHT im Git-Repo – sie werden für das Release automatisch geholt (CI) oder
# von Entwicklern einmalig mit diesem Skript.
#
#   ./scripts/fetch-ocr-assets.sh
#
# Danach enthält die Erweiterung die Bild-Anonymisierung (OCR).
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
DEST="$HERE/../extension/vendor/tesseract"
mkdir -p "$DEST/lang"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
cd "$TMP"

echo "Downloading Tesseract.js and language data via npm ..."
extract() { # <package> <target-dir>
    npm pack "$1" >/dev/null 2>&1
    local tgz; tgz="$(ls -1 *.tgz | head -1)"
    mkdir -p "$2"
    tar xzf "$tgz" -C "$2"
    rm -f "$tgz"
}

extract "tesseract.js@7" tjs
extract "tesseract.js-core@6" tcore
extract "@tesseract.js-data/eng" deng
extract "@tesseract.js-data/deu" ddeu

# Haupt-Skript + Worker
cp tjs/package/dist/tesseract.min.js "$DEST/"
cp tjs/package/dist/worker.min.js "$DEST/"

# Core: nur die LSTM-Varianten (wir nutzen die LSTM-Engine, oem=1).
# SIMD-LSTM als Standard, LSTM als Rückfall für ältere CPUs/Browser.
for v in tesseract-core-simd-lstm tesseract-core-lstm; do
    for ext in wasm wasm.js js; do
        [ -f "tcore/package/$v.$ext" ] && cp "tcore/package/$v.$ext" "$DEST/" || true
    done
done

# Sprachdaten (Standard 4.0.0)
cp deng/package/4.0.0/eng.traineddata.gz "$DEST/lang/"
cp ddeu/package/4.0.0/deu.traineddata.gz "$DEST/lang/"

echo "OCR assets ready in extension/vendor/tesseract/"
du -sh "$DEST" | awk '{print "Total size:", $1}'
