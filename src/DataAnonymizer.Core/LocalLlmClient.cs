using System.Text;
using System.Text.Json;

namespace DataAnonymizer.Services;

/// <summary>Einstellungen für das lokale LLM (Ollama).</summary>
public sealed class LocalLlmOptions
{
    /// <summary>Adresse des lokalen Ollama-Servers.</summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>Vorausgewähltes Modell, z.B. "llama3.2" oder "qwen2.5:3b".</summary>
    public string Model { get; set; } = "llama3.2";

    /// <summary>Maximale Wartezeit für eine Analyse (kleine Modelle auf CPU brauchen Zeit).</summary>
    public int TimeoutSeconds { get; set; } = 180;
}

/// <summary>
/// Spricht mit einem lokal laufenden LLM über die Ollama-API (http://localhost:11434).
/// Das Modell versteht, was persönliche Daten sind, und findet dadurch auch PII,
/// die kein Regex-Muster abdeckt – z.B. Namen ohne Anrede oder Firmennamen.
/// Es verlässt kein einziges Zeichen den Rechner: Ollama läuft komplett lokal.
/// </summary>
public sealed class LocalLlmClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly LocalLlmOptions _options;

    public LocalLlmClient(LocalLlmOptions? options = null)
    {
        _options = options ?? new LocalLlmOptions();
        _http = new HttpClient
        {
            BaseAddress = new Uri(_options.Endpoint.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(Math.Max(10, _options.TimeoutSeconds))
        };
    }

    public string Endpoint => _options.Endpoint;
    public string DefaultModel => _options.Model;

    /// <summary>
    /// Liefert die installierten Ollama-Modelle oder eine leere Liste, wenn Ollama
    /// nicht erreichbar ist. Dient gleichzeitig als Verfügbarkeits-Check.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var quickTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            quickTimeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await _http.GetAsync("/api/tags", quickTimeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<string>();
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(quickTimeout.Token));
            if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return models.EnumerateArray()
                .Select(m => m.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Lässt das lokale Modell den Text nach persönlichen Daten durchsuchen.
    /// Wirft bei Verbindungs- oder Modellfehlern eine Exception – der Aufrufer
    /// entscheidet, ob er ohne LLM-Funde weitermacht.
    /// </summary>
    public async Task<IReadOnlyList<LlmEntity>> DetectPiiAsync(string text, string? model = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<LlmEntity>();
        }

        var body = new
        {
            model = string.IsNullOrWhiteSpace(model) ? _options.Model : model,
            stream = false,
            format = "json",
            options = new { temperature = 0 },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = text }
            }
        };

        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("/api/chat", content, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var answer = doc.RootElement.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var c)
            ? c.GetString() ?? string.Empty
            : string.Empty;

        return ParseEntities(answer);
    }

    /// <summary>
    /// Wandelt die JSON-Antwort des Modells in Entitäten um. Tolerant gegenüber
    /// Code-Fences oder Text um das JSON herum; Unbrauchbares wird ignoriert.
    /// </summary>
    public static IReadOnlyList<LlmEntity> ParseEntities(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return Array.Empty<LlmEntity>();
        }

        var json = ExtractJsonObject(answer);
        if (json is null)
        {
            return Array.Empty<LlmEntity>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<LlmEntity>();
            }

            var result = new List<LlmEntity>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in entities.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("text", out var textProp))
                {
                    continue;
                }
                var value = textProp.GetString()?.Trim();
                if (string.IsNullOrEmpty(value) || !seen.Add(value))
                {
                    continue;
                }
                var category = item.TryGetProperty("category", out var categoryProp)
                    ? MapCategory(categoryProp.GetString())
                    : PiiCategory.Begriff;
                result.Add(new LlmEntity(value, category));
            }
            return result;
        }
        catch (JsonException)
        {
            return Array.Empty<LlmEntity>();
        }
    }

    private static string? ExtractJsonObject(string answer)
    {
        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');
        return start >= 0 && end > start ? answer[start..(end + 1)] : null;
    }

    private static PiiCategory MapCategory(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "name" or "person" => PiiCategory.Name,
        "organization" or "organisation" or "company" => PiiCategory.Organisation,
        "email" or "e-mail" => PiiCategory.Email,
        "phone" or "telephone" => PiiCategory.Telefon,
        "address" or "street" => PiiCategory.Adresse,
        "location" or "city" or "place" => PiiCategory.Ort,
        "iban" or "bank" => PiiCategory.Iban,
        "id" or "number" or "reference" or "account" => PiiCategory.Referenz,
        "birthdate" or "date" or "dob" => PiiCategory.Datum,
        "plate" or "license_plate" => PiiCategory.Kennzeichen,
        "card" or "credit_card" => PiiCategory.Kreditkarte,
        "ssn" or "social_security" => PiiCategory.Ahv,
        _ => PiiCategory.Begriff
    };

    // Der System-Prompt bringt dem Modell bei, was als "persönliche Daten" gilt –
    // unabhängig von der Sprache des Eingabetexts.
    private const string SystemPrompt =
        """
        You are a privacy assistant. Find all personally identifiable information (PII)
        in the user's text. The text can be in any language (German, English, French,
        Italian, or others) - you understand them all.

        Respond ONLY with a JSON object in exactly this form:
        {"entities":[{"text":"<exact substring from the input>","category":"<category>"}]}

        Allowed categories:
        - "name": names of real people (with or without salutation)
        - "organization": names of companies, employers, associations, institutions
        - "email": e-mail addresses
        - "phone": phone or fax numbers
        - "address": street names with house numbers
        - "location": postal codes with towns or cities that reveal where someone lives
        - "id": customer, policy, case, insurance, social security or account numbers, IBANs, credit card numbers, license plates
        - "birthdate": dates of birth
        - "other": anything else that could identify a specific person

        Rules:
        - Copy "text" verbatim from the input, character for character.
        - List each distinct value once, even if it appears several times.
        - Do NOT report: generic words, job titles alone, country names, amounts of
          money, dates that are not birth dates, or placeholders like [NAME_1].
        - If there is no PII, respond with {"entities":[]}.
        """;

    public void Dispose() => _http.Dispose();
}
