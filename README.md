# 🔒 Data Anonymizer / Daten-Anonymisierer (Local LLM Placeholder)

[![Build & Test](https://github.com/TheWishin/Local-LLM-Placeholder/actions/workflows/build.yml/badge.svg)](https://github.com/TheWishin/Local-LLM-Placeholder/actions/workflows/build.yml)

A local Blazor web app that **detects personal data in case texts and replaces it
with placeholders** – so you can paste cases into Claude Code or other AI tools
in a privacy-compliant way.

**Everything runs 100% locally.** No database, no cloud, no external calls.
The mapping table (placeholder → original value) only exists in the memory of
the running browser session.

🇩🇪 *Deutsch: Eine lokale Web-App, die persönliche Daten in Falltexten erkennt und
durch Platzhalter ersetzt. Alles läuft zu 100 % lokal – die Oberfläche ist auf
Deutsch, Englisch, Französisch und Italienisch verfügbar (Auswahl oben rechts).*

## Multi-language

- **UI languages:** German, English, French, Italian – switchable at the top
  right, saved locally in the browser.
- **Detection:** the patterns understand texts in all four languages at the same
  time (e.g. *Herr Max Muster*, *Mr. John Smith*, *Monsieur Jean Dupont*,
  *Signor Mario Rossi*, "born on", "né le", "nato il", policy/case number
  keywords, street formats, US phone numbers and SSNs, …). Mixed-language texts
  work too.
- **Placeholders follow the UI language:** `[TELEFON_1]` (de), `[PHONE_1]` (en),
  `[TELEPHONE_1]` (fr), `[TELEFONO_1]` (it). The de-anonymization step uses the
  session's mapping table, so the round trip always works.

## 🤖 Local LLM detection (optional)

Patterns can't catch everything – a name without a salutation, a company name,
an unusual identifier. For that, the app can additionally use a **local LLM via
[Ollama](https://ollama.com)** that *understands what privacy data is* and
reports anything that could identify a person. Findings are merged with the
pattern results (patterns win on overlaps, since they are more precise for
structured data like IBANs).

Your text still never leaves your machine: Ollama runs entirely locally.
If Ollama isn't running, the app simply works with patterns only.

Setup:

1. Install Ollama from [ollama.com](https://ollama.com)
2. Pull a model, e.g. `ollama pull llama3.2` (any chat model works;
   `qwen2.5:3b` or `llama3.1:8b` give better results if your machine can run them)
3. Enable **"AI detection (local LLM)"** in the app's sidebar and pick the model

Endpoint and default model can be changed in
`src/DataAnonymizer/appsettings.json` under `LocalLlm`.

## Workflow

1. **Anonymize:** paste the case text → "Anonymize" → copy the anonymized text.
2. **Use in Claude Code:** paste the anonymized text and let it work out the answer.
3. **De-anonymize:** copy the answer back into the app → "Restore original
   values" → the original values are put back in.

## What is detected?

| Category | Example | Placeholder (de/en) |
|---|---|---|
| Names (after Mr/Mrs/Herr/Frau/M./Sig., "Name:", "Customer:", …) | Herr Max Muster, Mr. John Smith | `[NAME_1]` |
| E-mail addresses | max.muster@example.ch | `[EMAIL_1]` |
| Phone numbers (international, CH, US) | +41 79 123 45 67, (555) 123-4567 | `[TELEFON_1]` / `[PHONE_1]` |
| Streets & house numbers (DE/FR/IT/EN) | Bahnhofstrasse 12a, 12 Main Street | `[ADRESSE_1]` / `[ADDRESS_1]` |
| Postal codes & towns | 8001 Zürich | `[ORT_1]` / `[CITY_1]` |
| IBAN | CH93 0076 2011 … | `[IBAN_1]` |
| Social security numbers (CH AHV, US SSN) | 756.1234.5678.97, 078-05-1120 | `[AHV_1]` / `[SSN_1]` |
| Credit cards (with Luhn check) | 4539 1488 0343 6467 | `[KARTE_1]` / `[CARD_1]` |
| Customer/policy/case numbers | Policy No. P-2023/4711 | `[REFERENZ_1]` / `[REFERENCE_1]` |
| License plates (CH cantons) | ZH 456789 | `[KENNZEICHEN_1]` / `[PLATE_1]` |
| Birth dates (optional: all dates) | geb. 12.03.1985, born on 12/03/1985 | `[DATUM_1]` / `[DATE_1]` |
| Companies & organizations (via local LLM) | Contoso AG | `[FIRMA_1]` / `[COMPANY_1]` |
| Custom terms (companies, projects, …) | Muster AG | `[BEGRIFF_1]` / `[TERM_1]` |

Each category can be toggled individually. Same original value → always the
same placeholder, so the text stays consistent.

## Getting started

### Option 1: Prebuilt package (no .NET required)

Download the ZIP for your system from
[Releases](https://github.com/TheWishin/Local-LLM-Placeholder/releases), unpack
it and run `DataAnonymizer.exe` (Windows) or `./DataAnonymizer` (macOS/Linux).
Then open **http://localhost:5100** in your browser.

### Option 2: Start script (with .NET 8 SDK)

- **Windows:** double-click `start.bat`
- **macOS/Linux:** `./start.sh`

The script starts the app and opens the browser automatically.

### Option 3: Manual

```bash
dotnet run --project src/DataAnonymizer
```

Then open http://localhost:5100.

### For the whole team (company network)

One person starts the app on the local network – the data never leaves the network:

```bash
dotnet run --project src/DataAnonymizer --urls http://0.0.0.0:5100
```

Everyone else opens `http://<hostname>:5100` in their browser.
(Note: the optional LLM detection talks to the Ollama instance on the machine
that runs the app.)

## Publishing a new release

Push a version tag – GitHub Actions automatically builds and publishes the
packages for Windows, Linux and macOS:

```bash
git tag v1.0.0
git push origin v1.0.0
```

## Project structure

```
src/DataAnonymizer/          Blazor web app (UI, 4 languages)
src/DataAnonymizer.Core/     Detection & replacement logic + Ollama client (no web dependencies)
tests/DataAnonymizer.Tests/  Automated tests (dotnet run --project tests/DataAnonymizer.Tests)
```

## Important note

Detection is based on patterns (regex) and optionally a local LLM. It is an
aid, **not a substitute for your own review**: skim the anonymized text before
pasting it into an AI tool. Names without a salutation or keyword are only
found when LLM detection is enabled – or via the "Custom terms" list (stored
locally in the browser).
