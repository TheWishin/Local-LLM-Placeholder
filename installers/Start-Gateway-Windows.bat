@echo off
REM ============================================================
REM  Daten-Anonymisierer - API-Gateway (Windows)
REM  Startet das Gateway auf http://localhost:8080
REM ============================================================

REM Ziel-Server (echter KI-Server). In der Regel unveraendert lassen.
if "%ANONYMIZER_UPSTREAM%"=="" set ANONYMIZER_UPSTREAM=https://api.anthropic.com

REM Optional: haus-internes Ollama zusaetzlich zur Muster-Erkennung nutzen.
REM set OLLAMA_HOST=http://ollama.firma.intern:11434
REM set ANONYMIZER_USE_LLM=true

echo.
echo   Gateway laeuft gleich auf: http://localhost:8080
echo   Richte deinen KI-Client so aus:
echo       ANTHROPIC_BASE_URL = http://localhost:8080
echo       ANTHROPIC_API_KEY  = dein echter Schluessel
echo.

DataAnonymizer.Proxy.exe --urls http://localhost:8080
pause
