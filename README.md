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

## 🤖 Local LLM detection – zero config

Patterns can't catch everything – a name without a salutation, a company name,
an unusual identifier. For that, the app additionally uses a **local LLM via
[Ollama](https://ollama.com)** that *understands what privacy data is* and
reports anything that could identify a person. Findings are merged with the
pattern results (patterns win on overlaps, since they are more precise for
structured data like IBANs).

**It just works:** install [Ollama](https://ollama.com), start the app – done.

- AI detection is **enabled by default** and activates itself as soon as the
  app finds Ollama on this machine (it also re-checks when you click
  Anonymize, so starting Ollama later is picked up without a reload).
- If no model is installed yet, the app **downloads the default model
  automatically** (one time, with a progress bar) and warms it up so the first
  analysis starts fast. No terminal commands needed.
- The start scripts (`start.sh` / `start.bat`) even launch Ollama in the
  background if it's installed but not running.
- Without Ollama, the app simply works with patterns only.

Your text still never leaves your machine: Ollama runs entirely locally.
Any installed chat model can be picked in the sidebar (`qwen2.5:3b` or
`llama3.1:8b` give better results than the default if your machine can run
them). Endpoint and default model can be changed in
`src/DataAnonymizer/appsettings.json` under `LocalLlm`.

## 🧩 Browser extension (Chrome & Edge, Mac & Windows)

The same anonymizer is also available as a **browser extension** – no .NET, no
server, nothing to keep running. It works in Google Chrome and Microsoft Edge
on macOS and Windows, and it installs without admin rights, so it's ideal for a
locked-down work laptop. Everything runs inside the browser; the optional AI
detection talks only to Ollama on `localhost`.

**Install (Chrome):**

1. Download `DataAnonymizer-…-browser-extension.zip` from
   [Releases](https://github.com/TheWishin/Local-LLM-Placeholder/releases)
   (or use the `extension/` folder of this repository) and unpack it.
2. Open `chrome://extensions`, switch on **Developer mode** (top right).
3. Click **Load unpacked** and select the unpacked `extension` folder.
4. Pin the 🔒 icon to the toolbar – done.

**Install (Edge):** same steps via `edge://extensions` → **Developer mode** →
**Load unpacked**. (If your company blocks developer mode in Edge by policy,
use Chrome or the desktop app instead.)

The popup speaks German, English, French and Italian, has the same detection
rules, custom terms, allowed values (👁 click-to-allow) and placeholder
languages as the app, and de-anonymizes answers again. The mapping table lives only in the browser session and is
discarded when the browser closes. If Ollama is installed, the extension finds
it automatically and downloads the default model on first use – exactly like
the app.

## Workflow

1. **Anonymize:** paste the case text → "Anonymize" → copy the anonymized text.
2. **Use in Claude Code:** paste the anonymized text and let it work out the
   answer – prose, an SQL script, code, anything containing the placeholders.
3. **De-anonymize:** copy the answer back into the app → "Restore original
   values" → the original values are put back in.

The round trip is built for real work with AI tools:

- **The mapping table is kept** locally in your browser until you delete it –
  so when Claude writes you an SQL script with `[NAME_1]` placeholders today,
  you can still translate it back tomorrow, after a restart. De-anonymizing
  works standalone, without running an anonymization first.
- **Tolerant restore:** placeholders reformatted by AI tools – `[ name_1 ]`,
  `[Name_1]` – are recognized too.
- **Export/import:** save a mapping as a JSON file and load it again later or
  on another machine. The file format is identical between the app and the
  browser extension, so a mapping created in one can be restored in the other.

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

**Your own rules in both directions** – available in the app *and* the
extension alike:

- **Custom terms** are always replaced, even when no pattern matches
  (company names, project names, names without a salutation).
- **Allowed values** are never replaced, even when detection finds them:
  click **👁** next to any entry in the mapping table to keep that value
  visible – the text updates immediately. Allowed values appear as green
  chips (click ✕ to replace them again) and can also be edited as a list.
  Both lists are stored locally in your browser.

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
extension/                   Chrome/Edge extension (same engine ported to JavaScript;
                             tests: node extension/engine.test.mjs)
```

The C# core and the JavaScript engine implement the same detection rules –
when changing one, change the other (`AnonymizerService.cs` ↔ `engine.js`,
`LocalLlmClient.cs` ↔ `ollama.js`) and run both test suites.

## Important note

Detection is based on patterns (regex) and optionally a local LLM. It is an
aid, **not a substitute for your own review**: skim the anonymized text before
pasting it into an AI tool. Names without a salutation or keyword are only
found when LLM detection is enabled – or via the "Custom terms" list (stored
locally in the browser).

- Wishin
