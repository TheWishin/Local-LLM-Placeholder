using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using DataAnonymizer.Proxy;
using DataAnonymizer.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Konfiguration (appsettings.json + Umgebungsvariablen) ----------------
var proxyOptions = new ProxyOptions();
builder.Configuration.GetSection("Proxy").Bind(proxyOptions);

// Umgebungsvariablen haben Vorrang – so kann die IT das Gateway ohne Dateiänderung
// betreiben (Docker/Systemd): ANONYMIZER_UPSTREAM, ANONYMIZER_LANGUAGE, ANONYMIZER_USE_LLM.
proxyOptions.Upstream = FirstNonEmpty(Environment.GetEnvironmentVariable("ANONYMIZER_UPSTREAM"), proxyOptions.Upstream);
proxyOptions.Language = FirstNonEmpty(Environment.GetEnvironmentVariable("ANONYMIZER_LANGUAGE"), proxyOptions.Language);
if (bool.TryParse(Environment.GetEnvironmentVariable("ANONYMIZER_USE_LLM"), out var useLlmEnv))
{
    proxyOptions.UseLlm = useLlmEnv;
}
if (bool.TryParse(Environment.GetEnvironmentVariable("ANONYMIZER_AUDIT"), out var auditEnv))
{
    proxyOptions.Audit = auditEnv;
}
proxyOptions.Upstream = proxyOptions.Upstream.TrimEnd('/');

// Lokales/haus-internes LLM (Ollama). Endpoint auch per OLLAMA_HOST/OLLAMA_BASE_URL.
var llmOptions = new LocalLlmOptions();
builder.Configuration.GetSection("LocalLlm").Bind(llmOptions);
llmOptions.Endpoint = FirstNonEmpty(
    Environment.GetEnvironmentVariable("OLLAMA_HOST"),
    Environment.GetEnvironmentVariable("OLLAMA_BASE_URL"),
    llmOptions.Endpoint);
llmOptions.Model = FirstNonEmpty(Environment.GetEnvironmentVariable("OLLAMA_MODEL"), llmOptions.Model);

builder.Services.AddSingleton(proxyOptions);
builder.Services.AddSingleton(llmOptions);
builder.Services.AddSingleton<AnonymizerService>();
builder.Services.AddSingleton<LocalLlmClient>();

// HttpClient zum echten KI-Server: kein Timeout (Streaming kann lange dauern),
// automatische Dekomprimierung an, damit wir den Text zurückübersetzen können.
builder.Services.AddHttpClient("upstream", c => c.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    });

var app = builder.Build();

var forwarder = new AnthropicForwarder(
    app.Services.GetRequiredService<IHttpClientFactory>(),
    app.Services.GetRequiredService<AnonymizerService>(),
    app.Services.GetRequiredService<LocalLlmClient>(),
    proxyOptions,
    app.Logger);

// ---- Endpunkte ------------------------------------------------------------

app.MapGet("/health", () => Results.Json(new { status = "ok", upstream = proxyOptions.Upstream }));

app.MapGet("/", () => Results.Content(InfoPage.Html(proxyOptions), "text/html; charset=utf-8"));

// Der eigentliche anonymisierende Round-Trip.
app.MapPost("/v1/messages", forwarder.HandleMessagesAsync);

// Alles andere unter /v1/* unverändert durchreichen, damit das Gateway ein
// vollwertiger Ersatz der Basis-URL ist (z.B. /v1/models, count_tokens).
app.Map("/v1/{**path}", forwarder.PassthroughAsync);

app.Logger.LogInformation("Anonymisierungs-Gateway läuft. Upstream: {Upstream}. LLM aktiv: {UseLlm} ({Endpoint}).",
    proxyOptions.Upstream, proxyOptions.UseLlm, llmOptions.Endpoint);

app.Run();

static string FirstNonEmpty(params string?[] values)
    => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
