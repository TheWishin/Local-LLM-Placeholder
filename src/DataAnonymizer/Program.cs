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

app.Run();
 
