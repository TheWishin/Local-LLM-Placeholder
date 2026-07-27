namespace DataAnonymizer.Proxy;

/// <summary>Kleine Statusseite unter "/", die erklärt, wie man das Gateway benutzt.</summary>
internal static class InfoPage
{
    public static string Html(ProxyOptions options) =>
        $$"""
        <!doctype html>
        <html lang="de">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Daten-Anonymisierer · API-Gateway</title>
          <style>
            :root { color-scheme: light dark; }
            body { font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
                   max-width: 760px; margin: 2.5rem auto; padding: 0 1.2rem; line-height: 1.55; }
            h1 { font-size: 1.5rem; } code, pre { background: rgba(127,127,127,.15); border-radius: 6px; }
            code { padding: .1rem .35rem; } pre { padding: .9rem 1rem; overflow-x: auto; }
            .ok { color: #1a7f37; font-weight: 600; }
            .muted { opacity: .75; font-size: .92rem; }
          </style>
        </head>
        <body>
          <h1>🔒 Daten-Anonymisierer · API-Gateway</h1>
          <p class="ok">Läuft. Anfragen an <code>/v1/messages</code> werden anonymisiert an den KI-Server
             weitergeleitet und die Antwort wird wieder zurückübersetzt.</p>
          <p>Ziel-Server (Upstream): <code>{{options.Upstream}}</code><br>
             Platzhalter-Sprache: <code>{{options.Language}}</code> ·
             Lokales LLM zusätzlich: <code>{{(options.UseLlm ? "an" : "aus")}}</code></p>

          <h2>So benutzen</h2>
          <p>Richte deinen Anthropic-Client auf dieses Gateway statt auf <code>api.anthropic.com</code>:</p>
          <pre>export ANTHROPIC_BASE_URL="http://localhost:8080"
        export ANTHROPIC_API_KEY="sk-ant-..."   # dein echter Schlüssel</pre>
          <p class="muted">Funktioniert mit dem Anthropic-SDK, Claude Code und allen Tools, bei denen sich die
             Basis-URL einstellen lässt. Der Schlüssel wird nur durchgereicht, nie gespeichert.</p>

          <h2>Was passiert</h2>
          <ol>
            <li>Deine App schickt den Text an dieses Gateway.</li>
            <li>Das Gateway ersetzt persönliche Daten durch Platzhalter (<code>[NAME_1]</code> …).</li>
            <li>Nur der anonymisierte Text geht an den KI-Server.</li>
            <li>Die Antwort kommt zurück, das Gateway setzt die Originalwerte wieder ein – auch in
                SQL-Skripten und Tool-Ausgaben.</li>
          </ol>
          <p class="muted">Hinweis: Die offizielle Claude-Desktop/Web-App lässt sich technisch nicht auf einen
             eigenen Server umleiten. Dieses Gateway ist für API-basierte Nutzung gedacht.</p>
        </body>
        </html>
        """;
}
