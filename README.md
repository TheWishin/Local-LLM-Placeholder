# 🔒 Daten-Anonymisierer (Local LLM Placeholder)

[![Build & Test](https://github.com/TheWishin/Local-LLM-Placeholder/actions/workflows/build.yml/badge.svg)](https://github.com/TheWishin/Local-LLM-Placeholder/actions/workflows/build.yml)

Eine lokale Blazor-Web-App, die **persönliche Daten in Falltexten erkennt und durch
Platzhalter ersetzt** – damit du Fälle datenschutzkonform in Claude Code oder andere
KI-Tools einfügen kannst.

**Alles läuft zu 100 % lokal.** Keine Datenbank, keine Cloud, keine externen Aufrufe.
Die Zuordnungstabelle (Platzhalter → Originalwert) existiert nur im Speicher der
laufenden Browser-Sitzung.

## Arbeitsablauf

1. **Anonymisieren:** Falltext einfügen → „Anonymisieren“ → anonymisierten Text kopieren.
2. **In Claude Code verwenden:** Den anonymisierten Text einfügen und die Antwort erarbeiten lassen.
3. **De-Anonymisieren:** Die Antwort zurück in die App kopieren → „Platzhalter zurückersetzen“ →
   die Originalwerte werden wieder eingesetzt.

## Was wird erkannt?

| Kategorie | Beispiel | Platzhalter |
|---|---|---|
| Namen (nach Herr/Frau, „Name:“, „Kunde:“, …) | Herr Max Muster | `[NAME_1]` |
| E-Mail-Adressen | max.muster@example.ch | `[EMAIL_1]` |
| Telefonnummern (CH/international) | +41 79 123 45 67 | `[TELEFON_1]` |
| Strassen & Hausnummern (DE/FR/IT) | Bahnhofstrasse 12a | `[ADRESSE_1]` |
| PLZ & Ortschaften | 8001 Zürich | `[ORT_1]` |
| IBAN | CH93 0076 2011 … | `[IBAN_1]` |
| AHV-Nummern | 756.1234.5678.97 | `[AHV_1]` |
| Kreditkarten (mit Luhn-Prüfung) | 4539 1488 0343 6467 | `[KARTE_1]` |
| Kunden-/Policen-/Fall-Nummern | Policen-Nr. P-2023/4711 | `[REFERENZ_1]` |
| Autokennzeichen (CH-Kantone) | ZH 456789 | `[KENNZEICHEN_1]` |
| Geburtsdaten (optional: alle Daten) | geb. 12.03.1985 | `[DATUM_1]` |
| Eigene Begriffe (Firmen, Projekte, …) | Muster AG | `[BEGRIFF_1]` |

Jede Kategorie ist einzeln ein-/ausschaltbar. Gleicher Originalwert → immer derselbe
Platzhalter, damit der Text konsistent bleibt.

## Starten

### Variante 1: Fertiges Paket (kein .NET nötig)

Unter [Releases](https://github.com/TheWishin/Local-LLM-Placeholder/releases) das ZIP
für dein System herunterladen, entpacken und `DataAnonymizer.exe` (Windows) bzw.
`./DataAnonymizer` (macOS/Linux) starten. Danach im Browser öffnen:
**http://localhost:5100**

### Variante 2: Start-Skript (mit .NET 8 SDK)

- **Windows:** `start.bat` doppelklicken
- **macOS/Linux:** `./start.sh`

Das Skript startet die App und öffnet den Browser automatisch.

### Variante 3: Manuell

```bash
dotnet run --project src/DataAnonymizer
```

Danach http://localhost:5100 öffnen.

### Für das ganze Team (Firmennetz)

Eine Person startet die App im lokalen Netz – die Daten verlassen das Netzwerk nicht:

```bash
dotnet run --project src/DataAnonymizer --urls http://0.0.0.0:5100
```

Alle anderen öffnen `http://<rechnername>:5100` im Browser.

## Neues Release veröffentlichen

Einen Versions-Tag pushen – GitHub Actions baut und veröffentlicht automatisch die
Pakete für Windows, Linux und macOS:

```bash
git tag v1.0.0
git push origin v1.0.0
```

## Projektstruktur

```
src/DataAnonymizer/       Blazor-Web-App (UI)
src/DataAnonymizer.Core/  Erkennungs- und Ersetzungslogik (ohne Web-Abhängigkeiten)
tests/DataAnonymizer.Tests/  Automatische Tests (dotnet run --project tests/DataAnonymizer.Tests)
```

## Wichtiger Hinweis

Die Erkennung basiert auf Mustern (Regex) und ist eine Hilfe, **kein Ersatz für die
eigene Kontrolle**: Vor dem Einfügen in ein KI-Tool den anonymisierten Text kurz
durchlesen. Namen ohne Anrede oder Schlüsselwort erkennt die App nicht automatisch –
dafür gibt es die Liste „Eigene Begriffe“ (wird lokal im Browser gespeichert).
