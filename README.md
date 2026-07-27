# 🔒 Data Anonymizer / Daten-Anonymisierer (Local LLM Placeholder)

[![Build & Test](https://github.com/TheWishin/Local-LLM-Placeholder/actions/workflows/build.yml/badge.svg)](https://github.com/TheWishin/Local-LLM-Placeholder/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)

> **Free & open source (MIT).** Copy it, change it, rebrand it, ship your own
> version. See **[Make your own version](CONTRIBUTING.md#make-your-own-version-fork-guide)**.

A local Blazor web app that **detects personal data in case texts and replaces it
with placeholders** – so you can paste cases into Claude Code or other AI tools
in a privacy-compliant way.

**Everything runs 100% locally.** No database, no cloud, no external calls.
The mapping table (placeholder → original value) only exists in the memory of
the running browser session.

🇩🇪 *Deutsch: Eine lokale Web-App, die persönliche Daten in Falltexten erkennt und
durch Platzhalter ersetzt. Alles läuft zu 100 % lokal – die Oberfläche ist auf
Deutsch, Englisch, Französisch und Italienisch verfügbar (Auswahl oben rechts).*

## For testers – quickest way to try it

Everything is on the
[Releases page](https://github.com/TheWishin/Local-LLM-Placeholder/releases/latest).

### Desktop app – one click, everything set up for you

1. Download the ZIP for your system and unzip it:
   - **Windows:** `...-win-x64.zip`
   - **Mac (Apple Silicon, M1–M4):** `...-osx-arm64.zip`
   - **Mac (Intel):** `...-osx-x64.zip`
2. Run the one-click setup inside the folder:
   - **Windows:** double-click **`Install-Windows.bat`**
   - **Mac:** double-click **`Install-macOS.command`**
3. That's it. The first time, it **installs the local AI (Ollama) by itself**,
   downloads a small model, starts the app and opens your browser at
   <http://localhost:5100>. Next time it starts instantly. A `START HERE.txt`
   in the folder explains everything.

Just want the app without the AI? Run `DataAnonymizer` directly – pattern
detection still works, the optional AI simply stays off until Ollama is present.

### Browser extension (Chrome/Edge)

Download the `browser-extension` ZIP, unzip it, open `chrome://extensions`, turn
on **Developer mode**, click **Load unpacked** and pick the folder. Click the 🔒
icon to use it. Installs without admin rights, so it works on a locked-down work
laptop.

The app opens with a short **"How it works"** strip (1 Anonymize → 2 Use in your
AI tool → 3 Translate back). Everything runs 100% locally; the optional AI
(Ollama) does too.

> **After updating the unpacked extension, click the ↻ reload icon on the
> extension card in `chrome://extensions`** – Chrome does not auto-update
> unpacked extensions. Then close and reopen the popup.
>
> **Troubleshooting:**
> - *Anonymize works but AI shows "not reachable":* install & start
>   [Ollama](https://ollama.com), then click **↻ Check again**. The extension
>   does not download the model itself (a popup can't hold a multi-GB download);
>   run `ollama pull llama3.2` once, or use the desktop app which downloads it
>   for you. Pattern detection always works without the AI.
> - *Paste button does nothing:* allow clipboard access when Chrome asks (the
>   extension needs `clipboardRead` for the Paste button – v1.5.1+).

**🖼️ Anonymize images (OCR):** open the extension, expand *"Anonymize image
(OCR)"*, pick a screenshot or scan – the extension reads the text **locally**
(Tesseract.js, bundled), finds the personal data and **blacks it out** in the
image, which you can then download. Nothing is uploaded. The OCR files (~30 MB,
German + English) ship inside the released extension ZIP; when working from
source, run `./scripts/fetch-ocr-assets.sh` once to add them.

**Right-click anywhere:** select text on any web page (e.g. in a ChatGPT or
Claude input box), right-click, and choose **"🔒 Anonymize selection (copy)"** –
the anonymized text is put on your clipboard, ready to paste back. Later,
**"🔓 Restore selection (copy)"** turns the AI's answer back into the real
values. A notification tells you how many values were replaced (and warns you if
any special-category data was involved). This uses fast pattern detection and
shares its mapping with the popup, so the two work together.

## Feature matrix

Both editions share the same detection engine; each also has a few features that
only make sense on its platform. See the [changelog](CHANGELOG.md) for the
version history.

| Feature | Desktop app | Browser extension |
|---|:---:|:---:|
| Text anonymize / restore (de-anonymize) | ✅ | ✅ |
| 4 UI languages (DE / EN / FR / IT) | ✅ | ✅ |
| Local-LLM (Ollama) detection, zero-config | ✅ | ✅ |
| Special-category (revDSG) data detection | ✅ | ✅ |
| Persistent mapping + export / import (JSON) | ✅ | ✅ |
| Allowed values (👁 click-to-allow) & custom terms | ✅ | ✅ |
| Right-click context menu (anonymize/restore selection) | — | ✅ |
| Image OCR redaction (black out PII in screenshots) | — | ✅ |
| One-click installers (auto-installs Ollama) | ✅ | — |

✅ = available · — = not applicable to this edition.

## Built for Swiss data protection (revDSG / nFADP)

The tool is designed to help you work with personal data in line with the
**revised Swiss Federal Act on Data Protection (revDSG, in force since Sept 2023)**
and the EU GDPR – by keeping the data on your machine and stripping it out
before anything reaches an external AI service. It pays particular attention to
**special-category data** (*besonders schützenswerte Personendaten*): health,
religion, political views, trade-union membership, biometric/genetic data, and
data on administrative or criminal proceedings – detected by the local AI and
replaced with a distinct `[SENSITIVE_n]` / `[SENSIBEL_n]` placeholder.

Swiss-specific detectors include the **AHV number**, the **health-insurance card
number**, the **UID** (`CHE-123.456.789`), canton **licence plates**, and
**IP addresses** (personal data under the revDSG).

*Detection is an aid and does not replace your own review. This is not legal advice.*

### revDSG data-category mapping

The revDSG distinguishes **ordinary personal data** from **special-category
personal data** (*besonders schützenswerte Personendaten*, Art. 5 lit. c
revDSG), which needs stricter handling. This is how the tool's detected
categories line up with that distinction:

| revDSG data category | Detected categories (placeholder) |
|---|---|
| **Special-category personal data** (*besonders schützenswerte Personendaten*) — health, religion/ideology, political or trade-union views, racial/ethnic origin, genetic & biometric data, administrative/criminal proceedings | Sensitive data (`[SENSITIVE_n]` / `[SENSIBEL_n]`, detected by the local LLM) |
| **Ordinary personal data** — identifies a person but is not special-category | Names (`[NAME]`), e-mail (`[EMAIL]`), phone (`[TELEFON]`/`[PHONE]`), street (`[ADRESSE]`/`[ADDRESS]`), postal code & town (`[ORT]`/`[CITY]`), birth date (`[DATUM]`/`[DATE]`), IBAN (`[IBAN]`), credit card (`[KARTE]`/`[CARD]`), AHV number / SSN (`[AHV]`/`[SSN]`), health-insurance card number (`[AHV]`/`[SSN]`), reference/policy/case numbers & UID (`[REFERENZ]`/`[REFERENCE]`), licence plates (`[KENNZEICHEN]`/`[PLATE]`), IP addresses (`[IP]`) |
| **Not personal data of a natural person** (still detectable) | Companies & organizations (`[FIRMA]`/`[COMPANY]`), custom terms (`[BEGRIFF]`/`[TERM]`) |

Only the LLM-driven *Sensitive data* category is flagged and warned about as
special-category. Some identifiers (e.g. the AHV number or the health-insurance
card number) can appear in health or social-assistance contexts; the tool
replaces them as ordinary identifiers, so classify them yourself when the
surrounding context is sensitive.

*This mapping is a practical aid, not legal advice — confirm the classification for your own use case.*

### Typical use cases

- **Insurance / claims:** paste a claim file, get advice or a letter drafted by
  an AI, translate the answer back – without the customer's data ever leaving.
- **Healthcare / HR:** case notes and reports with diagnoses or absences; the
  sensitive-data detection is built for exactly this.
- **Software & data teams:** let an AI write an **SQL script** against your
  schema using placeholders, then translate the script back and run it with the
  real values (the mapping is kept until you delete it – see below).
- **Support / IT tickets:** logs with IP addresses, emails and names.
- **Legal / admin dossiers:** references, case numbers, parties involved.

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

### Company-hosted Ollama (not only localhost)

Instead of running a model on every laptop, IT can host **one Ollama on a
server** and point everything at it:

- **Desktop app & gateway:** set the environment variables `OLLAMA_HOST`
  (e.g. `http://ollama.company.internal:11434`) and optionally `OLLAMA_MODEL`.
  They override `appsettings.json`, so no file edits are needed.
- **Browser extension:** enter the server address under *Settings → AI server
  (Ollama) address*. The extension asks for permission to reach that host once,
  then uses it for all AI detection.

The traffic to your internal server stays inside your network; nothing goes to
the public internet.

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

## 🌐 API-Gateway (transparent anonymizing proxy)

Besides the app and the extension there is a third way to use it: a local
**API gateway** that sits *between* your AI client and the real Claude server
and does the whole round trip for you automatically.

```
   your app ──▶ [gateway: anonymize] ──▶ Claude server
   your app ◀── [gateway: restore]   ◀── Claude server
```

Point any Anthropic-API client at the gateway instead of `api.anthropic.com`:

```bash
export ANTHROPIC_BASE_URL="http://localhost:8080"
export ANTHROPIC_API_KEY="sk-ant-..."   # your real key, only forwarded
```

- Confidential data (names, e-mails, IBAN, AHV, health details, …) is replaced
  with placeholders **before the request leaves your machine**, and the original
  values are put back into the answer.
- Works for **streaming (SSE)** responses and even restores placeholders inside
  **tool arguments** – so an AI-generated SQL script comes back with the real
  values already filled in.
- The API key is only passed through, never stored or logged. Everything runs
  locally; the optional company-hosted Ollama can be used for detection too
  (`ANONYMIZER_USE_LLM=true`).
- Config via environment variables: `ANONYMIZER_UPSTREAM`, `ANONYMIZER_LANGUAGE`
  (`En`/`De`/`Fr`/`It`), `ANONYMIZER_USE_LLM`, `OLLAMA_HOST`, `OLLAMA_MODEL`.

Ready-to-run, self-starting packages ship per platform
(`DataAnonymizer-Gateway-<version>-<os>.zip`) – unzip and run the start script.
Works with the Anthropic SDK, Claude Code and any tool with a configurable base
URL.

> **Note:** the official Claude desktop/web app (claude.ai) cannot be redirected
> to a custom server – that's a limitation of the app, not of the gateway. The
> gateway is for API-based usage; for the normal app, use the browser extension
> or the desktop app.

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
| Swiss UID (company ID) | CHE-123.456.789 | `[REFERENZ_1]` / `[REFERENCE_1]` |
| Health-insurance card number | 80756009012345678901 | `[AHV_1]` / `[SSN_1]` |
| IP addresses (IPv4 / IPv6) | 192.168.10.14 | `[IP_1]` |
| Birth dates (optional: all dates) | geb. 12.03.1985, born on 12/03/1985 | `[DATUM_1]` / `[DATE_1]` |
| Companies & organizations (via local LLM) | Contoso AG | `[FIRMA_1]` / `[COMPANY_1]` |
| **Special-category data** (health, religion, …) (via local LLM) | "Diagnose: Depression" | `[SENSIBEL_1]` / `[SENSITIVE_1]` |
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
src/DataAnonymizer.Proxy/    API gateway: Anthropic-compatible anonymizing reverse proxy
tests/DataAnonymizer.Tests/  Automated tests (dotnet run --project tests/DataAnonymizer.Tests)
extension/                   Chrome/Edge extension (same engine ported to JavaScript;
                             tests: node extension/engine.test.mjs)
```

The C# core and the JavaScript engine implement the same detection rules –
when changing one, change the other (`AnonymizerService.cs` ↔ `engine.js`,
`LocalLlmClient.cs` ↔ `ollama.js`) and run both test suites.

## Open source – make your own version

This project is **MIT-licensed**: you can fork it, rebrand it and release your
own build for free. The [CONTRIBUTING guide](CONTRIBUTING.md) has a short
**"Make your own version"** walkthrough (change the name/icons, add your own
detection rules, pick a different AI model, and let GitHub Actions build the
packages for you). Contributions – new detectors, translations, design – are
welcome.

The desktop app (C#) and the browser extension (JavaScript) share the same
detection logic on purpose, so a rule you add in one place is easy to mirror in
the other. Category colours, placeholder labels and all UI text are centralised
and localised in four languages.

## FAQ

**Is it really local?**
Yes. Detection, anonymization and de-anonymization all run on your machine – the
desktop app in your browser session, the extension inside the browser. The
mapping table (placeholder → original value) is never uploaded; the optional AI
talks only to Ollama on `localhost`.

**Does it need an internet connection?**
No, for the core work. The only things that touch the network are optional and
one-time: installing Ollama and downloading its model (the desktop installer can
do both for you), and – when building the extension from source – fetching the
OCR files. Your case texts and images never leave the machine.

**What if Ollama isn't installed?**
Everything still works with pattern detection. The optional AI simply stays off
until Ollama is present. The app and extension re-check when you click
*Anonymize*, so starting Ollama later is picked up without a reload.

**Is the AI required?**
No – it's optional. Patterns already cover structured data (e-mails, IBANs,
phone numbers, dates, …). The local LLM adds the things patterns miss: names
without a salutation, company names, and special-category (revDSG) data.

**How do I add my own terms?**
Use the **Custom terms** list (one per line) for values that should *always* be
replaced – company names, project names, a name with no salutation. Use
**Allowed values** for the opposite: click 👁 next to a mapping entry (or edit
the list) to keep a value visible so it is *never* replaced. Both lists are
stored locally and work in the app and the extension.

**How does the image OCR redaction work?**
It's in the browser extension: expand *"Anonymize image (OCR)"* and pick a
screenshot or scan. A bundled Tesseract.js worker reads the text **locally**
(German + English), the same detection runs on it, and the personal-data areas
are **blacked out** in the image for you to download. Nothing is uploaded. The
OCR files ship inside the released extension ZIP; from source, run
`./scripts/fetch-ocr-assets.sh` once.

**How do I make my own version?**
The project is MIT-licensed – fork it, rebrand it and ship your own build. The
[CONTRIBUTING guide](CONTRIBUTING.md#make-your-own-version-fork-guide) walks
through changing the name/icons, adding detection rules in both
`AnonymizerService.cs` and `extension/engine.js`, picking a different AI model,
and letting GitHub Actions build the packages.

## Important note

Detection is based on patterns (regex) and optionally a local LLM. It is an
aid, **not a substitute for your own review**: skim the anonymized text before
pasting it into an AI tool. Names without a salutation or keyword are only
found when LLM detection is enabled – or via the "Custom terms" list (stored
locally in the browser).

- Wishin
