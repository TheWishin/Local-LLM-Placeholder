#!/bin/bash
# =====================================================================
#  Data Anonymizer - macOS one-click setup
#  - installs Ollama (local AI) if it is missing
#  - downloads the AI model on first run
#  - starts the app and opens the browser
#  Double-click this file in Finder. If macOS blocks it, right-click ->
#  Open once, or run:  xattr -d com.apple.quarantine "Install-macOS.command"
# =====================================================================
cd "$(dirname "$0")" || exit 1

echo ""
echo "  ============================================"
echo "   Data Anonymizer - starting setup"
echo "  ============================================"
echo ""

# --- 1. Ensure Ollama (optional local AI) --------------------------------
if command -v ollama >/dev/null 2>&1; then
    echo "[OK] Ollama is already installed."
else
    echo "[..] Ollama not found - installing the local AI engine..."
    if command -v brew >/dev/null 2>&1; then
        brew install ollama || brew install --cask ollama
    else
        echo "[..] Homebrew not found. Opening the Ollama download page..."
        open "https://ollama.com/download/mac"
        echo "     Please install Ollama, then run this file again."
    fi
fi

# --- 2. Start Ollama in the background (ignored if already running) -------
if command -v ollama >/dev/null 2>&1; then
    if ! curl -s --max-time 2 http://localhost:11434/api/tags >/dev/null 2>&1; then
        echo "[..] Starting Ollama in the background..."
        (ollama serve >/dev/null 2>&1 &)
        sleep 2
    fi
    echo "[..] Making sure the AI model is available (first time can take a few minutes)..."
    (ollama pull llama3.2 >/dev/null 2>&1 &)
else
    echo "[!!] Ollama is not available - the app still works with pattern detection only."
fi

# --- 3. Architecture check (Apple Silicon vs Intel) ----------------------
ARCH="$(uname -m)"
if [ "$ARCH" = "arm64" ] && file ./DataAnonymizer 2>/dev/null | grep -q "x86_64"; then
    echo ""
    echo "[!!] This is the Intel build (x86_64), but your Mac is Apple Silicon ($ARCH)."
    echo "     It would only run slowly via Rosetta. For best speed on an M-series"
    echo "     MacBook (M1-M5), download the 'osx-arm64' package instead (native)."
    echo ""
fi

# --- 4. Start the app ----------------------------------------------------
echo ""
echo "[OK] Launching Data Anonymizer. Your browser will open at http://localhost:5100"
echo "     Keep this window open while you use the app. Close it (Ctrl+C) to stop."
echo ""
chmod +x ./DataAnonymizer 2>/dev/null
# Remove the quarantine flag from the whole folder so the unsigned app AND all
# its bundled libraries can run. Gatekeeper is stricter on Apple Silicon: a
# quarantined, unsigned binary is killed outright, so clear it recursively.
xattr -dr com.apple.quarantine "$(pwd)" 2>/dev/null
./DataAnonymizer
