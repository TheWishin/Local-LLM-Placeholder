using DataAnonymizer;
using DataAnonymizer.Components;
using DataAnonymizer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<AnonymizerService>();

// Lokales LLM (Ollama): Endpoint/Modell sind in appsettings.json konfigurierbar.
var llmOptions = new LocalLlmOptions();
builder.Configuration.GetSection("LocalLlm").Bind(llmOptions);
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
if (Environment.GetEnvironmentVariable("NO_BROWSER") != "1")
{
    var url = (app.Urls.FirstOrDefault()
               ?? app.Configuration["Urls"]
               ?? "http://localhost:5100").Split(';')[0].Replace("0.0.0.0", "localhost");
    app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(url));
}

app.Run();

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
 
