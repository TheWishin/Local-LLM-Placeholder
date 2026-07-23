@echo off
rem Startet den Daten-Anonymisierer lokal und oeffnet den Browser.
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo Fehler: .NET 8 SDK nicht gefunden.
    echo Download: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

set URL=http://localhost:5100
start "" cmd /c "timeout /t 3 /nobreak >nul && start %URL%"

echo Starte Daten-Anonymisierer auf %URL% (Beenden mit Ctrl+C) ...
dotnet run --project src\DataAnonymizer --urls %URL%
