# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project aims to follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
The desktop app (C#) and the browser extension (JavaScript) share the same
detection logic and are versioned together.

## [Unreleased]

## [1.8.0] - 2026-07-28

### Added
- **PDF-Anonymisierung in der Erweiterung.** Ein PDF auswählen – die Erweiterung
  liest den Text **lokal** (eingebettete Textebene via PDF.js, OCR nur für
  gescannte Seiten), erkennt dieselben persönlichen Daten wie beim Text und
  **schwärzt** sie auf jeder Seite. Heraus kommt ein neues, geschwärztes PDF zum
  Herunterladen. Nichts wird hochgeladen; die PDF.js-Bausteine liegen im Release
  bei (beim Arbeiten aus dem Quellcode `scripts/fetch-ocr-assets.sh` einmal
  ausführen).

### Performance
- **Schnellere Rückübersetzung.** Die De-Anonymisierung baut jetzt einmal eine
  Nachschlagetabelle und ersetzt in **einem** Durchgang, statt pro Platzhalter ein
  eigenes Regex zu kompilieren (vorher O(Text × Platzhalter), jetzt O(Text)).
  Spürbar vor allem im API-Gateway, das viele Felder einer Antwort zurückübersetzt
  – sowohl im C#- als auch im JavaScript-Motor.

## [1.7.2] - 2026-07-27

### Changed
- **Zwei Release-Varianten zur Auswahl:**
  - **ohne Gateway** (Tag `v1.7.2`): nur Desktop-App + Browser-Erweiterung – die
    Alltags-Werkzeuge, schlank.
  - **mit Gateway** (Tag `gateway-v1.7.2`): ein Komplettpaket, das zusätzlich das
    API-Gateway enthält (App + Erweiterung + Gateway zusammen).
  Beide Releases sind klar mit „ohne Gateway"/„mit Gateway" benannt; die
  Gateway-Dateinamen sind bereinigt (`DataAnonymizer-Gateway-v1.7.2-<os>.zip`).

## [1.7.1] - 2026-07-27

### Changed
- **Releases getrennt.** Die Desktop-App und die Browser-Erweiterung bilden wieder
  ein eigenes, schlankes Release (Tag `v*`) – wie gewohnt. Das **API-Gateway** hat
  ab sofort ein **eigenes, separates Release** (Workflow `release-gateway.yml`,
  Tag `gateway-v*`), weil es ein optionaler Baustein nur für die API-Nutzung ist.
  An App und Erweiterung selbst ändert sich nichts.

## [1.7.0] - 2026-07-27

### Added
- **API-Gateway (`DataAnonymizer.Proxy`), ein neues Release.** Ein lokaler,
  Anthropic-kompatibler Reverse-Proxy für den vollen Round-Trip
  *App → Gateway (anonymisieren) → Claude-Server → Gateway (zurückübersetzen) → App*.
  Der Client zeigt einfach mit `ANTHROPIC_BASE_URL` auf das Gateway; vertrauliche
  Daten werden durch Platzhalter ersetzt, bevor sie den Rechner verlassen, und in
  der Antwort wieder eingesetzt – auch im Streaming (SSE) und in Tool-Argumenten
  (z.B. von der KI erzeugte SQL-Skripte). Der API-Schlüssel wird nur durchgereicht,
  nie gespeichert. Fertige, selbst-startende Pakete für Windows/macOS/Linux.
- **Haus-internes Ollama (nicht nur localhost).** Die Ollama-Adresse ist jetzt
  konfigurierbar – in der Erweiterung über ein Feld in den Einstellungen (mit
  einmaliger Berechtigungsabfrage für den Server), in der Desktop-App und im
  Gateway über die Umgebungsvariablen `OLLAMA_HOST` / `OLLAMA_BASE_URL` /
  `OLLAMA_MODEL`. So kann die IT ein zentrales Ollama auf einem Server betreiben.
- **Docker/Compose für das Gateway.** `Dockerfile` und `docker-compose.yml` für
  den zentralen Betrieb auf einem haus-internen Server (`docker compose up -d`),
  inkl. optionalem Ollama-Service.
- **Datenschutzfreundliches Protokoll (opt-in, `ANONYMIZER_AUDIT=true`).** Das
  Gateway protokolliert pro Anfrage nur die Anzahl ersetzter Werte je Kategorie
  (z.B. „3 Platzhalter (name×1, email×1, iban×1)") – nie die Originalwerte. Das
  belegt die Wirksamkeit für die revDSG-Rechenschaftspflicht, ohne selbst neue
  Personendaten zu erzeugen.

### Notes
- Die offizielle Claude-Desktop/Web-App (claude.ai) lässt sich technisch nicht auf
  einen eigenen Server umleiten; das Gateway ist für API-basierte Nutzung gedacht
  (Anthropic-SDK, Claude Code, eigene Apps). Für die normale App bleiben die
  Browser-Erweiterung und die Desktop-App der Weg.

## [1.6.0] - 2026-07-26

### Added
- **Image anonymization (OCR) in the browser extension.** Pick a screenshot or
  scan and the extension reads the text **locally** with a bundled Tesseract.js
  worker (German + English), runs the same detection as the text engine, and
  **blacks out** the personal-data areas in the image for you to download.
  Nothing is uploaded. The OCR assets (~30 MB) ship inside the released
  extension ZIP; when working from source, run `scripts/fetch-ocr-assets.sh`
  once to add them.
- **New detectors in both engines:** vehicle identification numbers (VIN),
  BIC/SWIFT codes (keyword-anchored), and European VAT / USt-IdNr numbers
  (keyword-anchored). They reuse existing placeholder categories.
- CI now runs the extension image test suite (`extension/image.test.mjs`) and
  packages the OCR assets into the release extension ZIP.

## [1.5.1] - 2026-07-24

### Fixed
- Extension **Paste** button now works: added the `clipboardRead` permission so
  Chrome/Edge can read the clipboard on click.
- Fixed a crash in the right-click context menu.
- AI (Ollama) detection made non-blocking, so anonymization no longer stalls the
  popup while the local LLM is being checked.

## [1.5.0] - 2026-07-24

### Added
- **Special-category (revDSG) data warning:** a `⚠️` notice reports how many
  *besonders schützenswerte Personendaten* were detected so they can be reviewed
  with extra care.
- **Right-click context menu (extension):** select text on any web page and
  choose *"🔒 Anonymize selection (copy)"* or *"🔓 Restore selection (copy)"*.
  It uses fast pattern detection and shares its mapping with the popup.

## [1.4.0] - 2026-07-24

### Added
- **Copy and paste buttons** in the app and extension for a smoother round trip.
- **Open-sourced under the MIT license**, with a "Make your own version" fork
  guide in [CONTRIBUTING.md](CONTRIBUTING.md).

### Changed
- Visual polish across the app and popup.

## [1.3.0] - 2026-07-24

### Added
- **One-click installers** bundled into the desktop release (Windows, macOS,
  Linux): first run installs the local AI (Ollama) by itself, downloads a small
  model, starts the app and opens the browser at `http://localhost:5100`.
- Browser opens automatically when the app starts.
- **Swiss-revDSG-aware detectors:** UID (`CHE-123.456.789`), health-insurance
  card number, and IP addresses (IPv4/IPv6, personal data under the revDSG).

## [1.2.0] - 2026-07-24

### Changed
- Friendlier, easier-to-understand UI for non-technical testers, including a
  short **"How it works"** strip (1 Anonymize → 2 Use in your AI tool →
  3 Translate back).

## [1.1.0] - 2026-07-24

### Added
- **Persistent mapping table:** the placeholder round trip now works for SQL
  scripts and across later sessions — the mapping is kept locally until you
  delete it, so answers or scripts can be translated back the next day.
- Manual trigger for the release workflow (via *Run workflow*), alongside the
  push-a-tag trigger.

### Fixed
- Release build: renamed the `VERSION` environment variable to
  `RELEASE_VERSION` so MSBuild no longer rejects `v1.0.0` as a .NET version.

## [1.0.0] - 2026-07-24

Initial release.

### Added
- **Local Blazor desktop web app** that detects personal data in case texts and
  replaces it with consistent placeholders like `[NAME_1]` / `[EMAIL_2]`.
  Everything runs 100% locally; the mapping table lives only in memory.
- **Four UI languages** — German, English, French, Italian — with
  language-specific placeholder labels (`[TELEFON_1]` / `[PHONE_1]` /
  `[TELEPHONE_1]` / `[TELEFONO_1]`).
- **Pattern detection** across all four languages: names (after a salutation or
  keyword), e-mail addresses, phone numbers (CH/international/US), streets,
  postal codes & towns, IBAN, Swiss AHV number & US SSN, credit cards (Luhn
  check), reference/policy/case numbers, CH licence plates, and birth dates.
- **Optional local LLM (Ollama) detection, zero-config:** enabled by default and
  activated as soon as Ollama is found; catches names without a salutation,
  companies/organizations and free-text personal data. Missing models are
  downloaded automatically on first use. Without Ollama the app runs on patterns
  only.
- **De-anonymize round trip:** restore original values from an AI answer, with
  tolerant matching of reformatted placeholders (`[ name_1 ]`, `[Name_1]`).
- **Custom terms** (always replaced) and **allowed values** (never replaced;
  click 👁 in the mapping table to keep a value visible).
- **Chrome/Edge browser extension** with the same detection engine ported to
  JavaScript, the same four UI languages, custom terms and allowed values, and a
  mapping that lives in the browser session. Installs without admin rights.
- **Export/import** the mapping as a JSON file, with an identical format shared
  between the app and the extension.
- GitHub Actions build/test and release automation producing self-contained
  Windows/Linux/macOS packages and the browser-extension ZIP.

[Unreleased]: https://github.com/TheWishin/Local-LLM-Placeholder/compare/v1.8.0...HEAD
[1.8.0]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.8.0
[1.7.2]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.7.2
[1.7.1]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.7.1
[1.7.0]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.7.0
[1.6.0]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.6.0
[1.5.1]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.5.1
[1.5.0]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.5.0
[1.4.0]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.4.0
[1.3.0]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.3.0
[1.2.0]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.2.0
[1.1.0]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.1.0
[1.0.0]: https://github.com/TheWishin/Local-LLM-Placeholder/releases/tag/v1.0.0
