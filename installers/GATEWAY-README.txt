====================================================================
  Daten-Anonymisierer - API-Gateway ("Placeholder-API")
====================================================================

WAS IST DAS?
------------
Ein kleiner lokaler Server, der zwischen deiner KI-App und dem echten
KI-Server (Anthropic Claude) sitzt. Der Weg der Daten:

    App  ->  [Gateway: anonymisieren]  ->  Claude-Server
    App  <-  [Gateway: zurueckuebersetzen] <-  Claude-Server

Vertrauliche Daten (Namen, E-Mails, IBAN, AHV, Diagnosen ...) werden
durch Platzhalter wie [NAME_1] ersetzt, BEVOR sie den Rechner Richtung
KI-Server verlassen. Die Antwort kommt zurueck und das Gateway setzt die
echten Werte wieder ein - auch in SQL-Skripten und Tool-Ausgaben.


SO STARTEN
----------
Windows : Doppelklick auf  Start-Gateway-Windows.bat
macOS   : Doppelklick auf  start-gateway-macOS.command
Linux   : ./start-gateway-Linux.sh

Das Gateway laeuft dann auf  http://localhost:8080


SO BENUTZEN
-----------
Richte deinen Anthropic-Client auf das Gateway statt auf api.anthropic.com:

    ANTHROPIC_BASE_URL = http://localhost:8080
    ANTHROPIC_API_KEY  = dein echter Schluessel

Das funktioniert mit dem Anthropic-SDK, mit Claude Code und mit allen
Tools, bei denen sich die Basis-URL einstellen laesst. Dein Schluessel
wird nur durchgereicht, nie gespeichert.


EINSTELLUNGEN (Umgebungsvariablen, optional)
--------------------------------------------
ANONYMIZER_UPSTREAM   Ziel-Server (Standard: https://api.anthropic.com)
ANONYMIZER_LANGUAGE   Platzhalter-Sprache: En, De, Fr, It (Standard: En)
ANONYMIZER_USE_LLM    true = zusaetzlich lokales/haus-internes Ollama nutzen
OLLAMA_HOST           Adresse des Ollama-Servers (Standard: localhost:11434)
OLLAMA_MODEL          Modellname (Standard: llama3.2)


WICHTIGER HINWEIS
-----------------
Die offizielle Claude-Desktop-/Web-App (claude.ai) laesst sich technisch
NICHT auf einen eigenen Server umleiten. Dieses Gateway ist fuer die
API-basierte Nutzung gedacht (SDK, Claude Code, eigene Apps). Fuer die
normale App bleibt die Browser-Erweiterung bzw. die Desktop-App der Weg.

Alles laeuft lokal. Es werden keine Daten gespeichert oder protokolliert.
