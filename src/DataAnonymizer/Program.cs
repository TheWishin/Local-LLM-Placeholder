using System.Net;
using System.Net.Sockets;
using DataAnonymizer;
using DataAnonymizer.Components;
using DataAnonymizer.Services;

var builder = WebApplication.CreateBuilder(args);

// Adresse bestimmen und bei belegtem Port ausweichen. Ohne das bricht der Start
// mit einer Exception ab ("address already in use") – der häufigste Fall unter
// Windows, wenn die App noch ein zweites Mal gestartet wird oder ein anderes
// Programm den Port 5100 belegt.
var configuredUrl = (builder.Configuration["Urls"] ?? "http://localhost:5100")
    .Split(';')[0].Trim();
var listenUrl = FindFreeUrl(configuredUrl);
builder.WebHost.UseUrls(listenUrl);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<AnonymizerService>();

// Lokales LLM (Ollama): Endpoint/Modell sind in appsettings.json konfigurierbar.
// Zusätzlich per Umgebungsvariable, damit die IT ein haus-internes Ollama auf einem
// Server betreiben kann, ohne Dateien zu ändern: OLLAMA_HOST / OLLAMA_BASE_URL / OLLAMA_MODEL.
var llmOptions = new LocalLlmOptions();
builder.Configuration.GetSection("LocalLlm").Bind(llmOptions);
var envEndpoint = Environment.GetEnvironmentVariable("OLLAMA_HOST")
                  ?? Environment.GetEnvironmentVariable("OLLAMA_BASE_URL");
if (!string.IsNullOrWhiteSpace(envEndpoint))
{
    llmOptions.Endpoint = envEndpoint;
}
var envModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL");
if (!string.IsNullOrWhiteSpace(envModel))
{
    llmOptions.Model = envModel;
}
builder.Services.AddSingleton(llmOptions);
builder.Services.AddSingleton<LocalLlmClient>();

// Sprache wird pro Browser-Sitzung gewählt.
builder.Services.AddScoped<LocalizationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Beim Start automatisch den Browser öffnen, damit die App "einfach läuft"
// (Doppelklick auf das Programm genügt). Abschaltbar mit NO_BROWSER=1.
var browserUrl = listenUrl.Replace("0.0.0.0", "localhost").Replace("[::]", "localhost");
if (Environment.GetEnvironmentVariable("NO_BROWSER") != "1")
{
    app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(browserUrl));
}

Console.WriteLine();
Console.WriteLine($"  Daten-Anonymisierer laeuft auf: {browserUrl}");
Console.WriteLine("  Dieses Fenster offen lassen. Zum Beenden schliessen (oder Strg+C).");
Console.WriteLine();

try
{
    app.Run();
}
catch (Exception ex)
{
    // Ohne diese Behandlung sieht man unter Windows nur kurz einen Stapelspeicher-
    // Auszug, bevor sich das Fenster schliesst. Jetzt gibt es eine verständliche
    // Meldung, eine Logdatei und das Fenster bleibt offen.
    ReportStartupFailure(ex, listenUrl);
    return 1;
}
return 0;

/// <summary>
/// Liefert die konfigurierte Adresse zurück – oder, falls der Port belegt ist,
/// dieselbe Adresse mit dem nächsten freien Port. So startet die App auch dann,
/// wenn sie bereits läuft oder ein anderes Programm den Port benutzt.
/// </summary>
static string FindFreeUrl(string url)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return url;
    }
    if (IsPortFree(uri.Port))
    {
        return url;
    }

    for (var port = uri.Port + 1; port <= uri.Port + 20; port++)
    {
        if (!IsPortFree(port))
        {
            continue;
        }
        Console.WriteLine();
        Console.WriteLine($"  Hinweis: Port {uri.Port} ist belegt (laeuft die App vielleicht schon?).");
        Console.WriteLine($"  Es wird stattdessen Port {port} verwendet.");
        return new UriBuilder(uri) { Port = port }.Uri.ToString().TrimEnd('/');
    }
    return url;   // Nichts frei gefunden – normal starten und Kestrel melden lassen.
}

static bool IsPortFree(int port)
{
    try
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        listener.Stop();
        return true;
    }
    catch (SocketException)
    {
        return false;
    }
}

/// <summary>
/// Zeigt einen Startfehler verständlich an, schreibt ihn in eine Logdatei und
/// hält das Fenster offen, damit die Meldung lesbar bleibt.
/// </summary>
static void ReportStartupFailure(Exception ex, string url)
{
    Console.WriteLine();
    Console.WriteLine("  ============================================");
    Console.WriteLine("   Der Daten-Anonymisierer konnte nicht starten");
    Console.WriteLine("  ============================================");
    Console.WriteLine();
    Console.WriteLine($"  Grund: {ex.Message}");
    Console.WriteLine();
    if (ex is IOException || ex.InnerException is SocketException || ex is SocketException)
    {
        Console.WriteLine($"  Die Adresse {url} liess sich nicht belegen.");
        Console.WriteLine("  Meist laeuft die App bereits in einem anderen Fenster.");
        Console.WriteLine("  Schliesse das andere Fenster und starte erneut.");
        Console.WriteLine();
    }

    var logPath = WriteErrorLog(ex);
    if (logPath is not null)
    {
        Console.WriteLine($"  Einzelheiten stehen in: {logPath}");
        Console.WriteLine();
    }

    // Fenster offen halten, damit die Meldung nicht sofort verschwindet.
    if (!Console.IsInputRedirected)
    {
        Console.WriteLine("  Zum Schliessen eine Taste druecken ...");
        try
        {
            Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
            // Keine Konsole verfügbar – dann eben ohne Warten beenden.
        }
    }
}

static string? WriteErrorLog(Exception ex)
{
    var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}";
    foreach (var folder in new[] { AppContext.BaseDirectory, Path.GetTempPath() })
    {
        try
        {
            var path = Path.Combine(folder, "DataAnonymizer-error.log");
            File.AppendAllText(path, text);
            return path;
        }
        catch (Exception)
        {
            // Ordner nicht beschreibbar (z.B. Programme-Verzeichnis) – nächsten versuchen.
        }
    }
    return null;
}

static void OpenBrowser(string url)
{
    try
    {
        if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }
        else if (OperatingSystem.IsMacOS())
        {
            System.Diagnostics.Process.Start("open", url);
        }
        else
        {
            System.Diagnostics.Process.Start("xdg-open", url);
        }
    }
    catch
    {
        // Kein Browser verfügbar (z.B. Server) – dann bitte manuell öffnen.
    }
}
 
