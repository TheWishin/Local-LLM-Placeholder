#!/bin/bash
# ============================================================
#  Daten-Anonymisierer - API-Gateway (macOS)
#  Startet das Gateway auf http://localhost:8080
# ============================================================
cd "$(dirname "$0")" || exit 1
chmod +x ./DataAnonymizer.Proxy 2>/dev/null

# Ziel-Server (echter KI-Server). In der Regel unveraendert lassen.
export ANONYMIZER_UPSTREAM="${ANONYMIZER_UPSTREAM:-https://api.anthropic.com}"

# Optional: haus-internes Ollama zusaetzlich nutzen:
#   export OLLAMA_HOST="http://ollama.firma.intern:11434"
#   export ANONYMIZER_USE_LLM=true

echo
echo "  Gateway laeuft gleich auf: http://localhost:8080"
echo "  Richte deinen KI-Client so aus:"
echo "      export ANTHROPIC_BASE_URL=http://localhost:8080"
echo "      export ANTHROPIC_API_KEY=dein-echter-schluessel"
echo

./DataAnonymizer.Proxy --urls http://localhost:8080
