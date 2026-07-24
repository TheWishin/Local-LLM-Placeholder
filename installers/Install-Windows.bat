@echo off
rem =====================================================================
rem  Data Anonymizer - Windows one-click setup
rem  - installs Ollama (local AI) if it is missing
rem  - downloads the AI model on first run
rem  - starts the app and opens the browser
rem  Double-click this file. No admin rights required for the app itself.
rem =====================================================================
setlocal
cd /d "%~dp0"
title Data Anonymizer - Setup

echo(
echo  ============================================
echo   Data Anonymizer - starting setup
echo  ============================================
echo(

rem --- 1. Ensure Ollama (optional local AI) --------------------------------
where ollama >nul 2>nul
if %errorlevel%==0 (
    echo [OK] Ollama is already installed.
) else (
    echo [..] Ollama not found - installing the local AI engine...
    where winget >nul 2>nul
    if %errorlevel%==0 (
        winget install --id Ollama.Ollama -e --accept-source-agreements --accept-package-agreements
    ) else (
        echo [..] winget not available - downloading the Ollama installer...
        powershell -NoProfile -Command "try { Invoke-WebRequest -Uri 'https://ollama.com/download/OllamaSetup.exe' -OutFile \"$env:TEMP\OllamaSetup.exe\" ; Start-Process -Wait \"$env:TEMP\OllamaSetup.exe\" } catch { Write-Host 'Automatic install failed. Please install Ollama manually from https://ollama.com' }"
    )
)

rem --- 2. Start Ollama in the background (ignored if already running) -------
where ollama >nul 2>nul
if %errorlevel%==0 (
    echo [..] Starting Ollama in the background...
    start "" /b ollama serve >nul 2>nul
    rem Pull the default model (skipped automatically if already present)
    echo [..] Making sure the AI model is available (first time can take a few minutes)...
    start "" /b ollama pull llama3.2 >nul 2>nul
) else (
    echo [!!] Ollama is not available - the app still works with pattern detection only.
)

rem --- 3. Start the app ----------------------------------------------------
echo(
echo [OK] Launching Data Anonymizer. Your browser will open at http://localhost:5100
echo      Keep this window open while you use the app. Close it to stop.
echo(
DataAnonymizer.exe

endlocal
