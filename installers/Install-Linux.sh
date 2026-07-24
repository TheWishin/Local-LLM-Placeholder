#!/bin/bash
# =====================================================================
#  Data Anonymizer - Linux one-click setup
#  - installs Ollama (local AI) if it is missing
#  - downloads the AI model on first run
#  - starts the app and opens the browser
#  Run:  ./Install-Linux.sh
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
    curl -fsSL https://ollama.com/install.sh | sh || \
        echo "[!!] Automatic install failed. See https://ollama.com for manual setup."
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

# --- 3. Start the app ----------------------------------------------------
echo ""
echo "[OK] Launching Data Anonymizer. Your browser will open at http://localhost:5100"
echo "     Keep this window open while you use the app. Close it (Ctrl+C) to stop."
echo ""
chmod +x ./DataAnonymizer 2>/dev/null
./DataAnonymizer
