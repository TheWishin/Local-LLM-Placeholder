using System.Text;
using Microsoft.AspNetCore.Http.Features;
using DataAnonymizer.Services;

namespace DataAnonymizer.Proxy;

/// <summary>
/// Leitet Anfragen an den echten KI-Server weiter und übernimmt dabei den
/// Anonymisierungs-Round-Trip für <c>/v1/messages</c>. Andere Pfade werden
/// unverändert durchgereicht, damit das Gateway ein vollwertiger Ersatz der
/// Basis-URL ist.
/// </summary>
public sealed class AnthropicForwarder
{
    // Hop-by-hop- und längenabhängige Header, die ein Proxy nicht kopieren darf.
    private static readonly HashSet<string> SkipRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Content-Length", "Connection", "Keep-Alive", "Transfer-Encoding",
        "Upgrade", "Proxy-Connection", "TE", "Trailer", "Accept-Encoding"
    };

    private static readonly HashSet<string> SkipResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Length", "Transfer-Encoding", "Content-Encoding", "Connection", "Keep-Alive", "Trailer"
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly AnonymizerService _service;
    private readonly LocalLlmClient _llm;
    private readonly ProxyOptions _options;
    private readonly ILogger _logger;

    public AnthropicForwarder(IHttpClientFactory httpFactory, AnonymizerService service, LocalLlmClient llm, ProxyOptions options, ILogger logger)
    {
        _httpFactory = httpFactory;
        _service = service;
        _llm = llm;
        _options = options;
        _logger = logger;
    }

    /// <summary>Anonymisierender Round-Trip für <c>POST /v1/messages</c>.</summary>
    public async Task HandleMessagesAsync(HttpContext context)
    {
        var ct = context.RequestAborted;

        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
        {
            requestBody = await reader.ReadToEndAsync(ct);
        }

        // Optional das lokale/haus-interne LLM befragen (findet Namen ohne Anrede, Firmen …).
        IReadOnlyCollection<LlmEntity>? llmFindings = null;
        if (_options.UseLlm)
        {
            llmFindings = await TryLlmFindingsAsync(requestBody, ct);
        }

        var anonymizerOptions = _options.BuildAnonymizerOptions();
        var rewrite = AnthropicRewriter.AnonymizeRequestBody(requestBody, _service, anonymizerOptions, llmFindings);
        var mappings = rewrite.Mappings;

        if (_options.Audit)
        {
            // Nur Zahlen je Kategorie – nie die Originalwerte.
            _logger.LogInformation("Anfrage anonymisiert: {Summary}", AuditSummary.Summarize(mappings));
        }

        var client = _httpFactory.CreateClient("upstream");
        using var upstreamRequest = new HttpRequestMessage(HttpMethod.Post, _options.Upstream + "/v1/messages" + context.Request.QueryString);
        CopyRequestHeaders(context, upstreamRequest);
        upstreamRequest.Content = new StringContent(rewrite.Json, Encoding.UTF8, "application/json");

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Weiterleitung an den KI-Server fehlgeschlagen.");
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(new { error = new { type = "gateway_error", message = "Upstream request failed: " + ex.Message } }, ct);
            return;
        }

        using (upstreamResponse)
        {
            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            CopyResponseHeaders(upstreamResponse, context);

            var isStream = string.Equals(upstreamResponse.Content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase);

            // Ohne erkannte PII gibt es nichts zurückzuübersetzen – unverändert durchreichen.
            if (mappings.Count == 0)
            {
                await using var raw = await upstreamResponse.Content.ReadAsStreamAsync(ct);
                await raw.CopyToAsync(context.Response.Body, ct);
                await context.Response.Body.FlushAsync(ct);
                return;
            }

            if (isStream)
            {
                await StreamBackAsync(upstreamResponse, context, mappings, ct);
            }
            else
            {
                var body = await upstreamResponse.Content.ReadAsStringAsync(ct);
                var restored = AnthropicRewriter.DeanonymizeResponseBody(body, mappings, _service);
                await context.Response.WriteAsync(restored, ct);
                await context.Response.Body.FlushAsync(ct);
            }
        }
    }

    private async Task StreamBackAsync(HttpResponseMessage upstreamResponse, HttpContext context, IReadOnlyList<MappingEntry> mappings, CancellationToken ct)
    {
        // Response-Pufferung abschalten: jedes Token soll sofort beim Client
        // ankommen (echtes Streaming), statt bis zu einer Puffergrösse zu warten.
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var sse = new SseDeanonymizer(mappings, _service);
        await using var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(upstreamStream, Encoding.UTF8);

        var buffer = new char[4096];
        int n;
        while ((n = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            var outText = sse.Push(new string(buffer, 0, n));
            if (outText.Length > 0)
            {
                await context.Response.WriteAsync(outText, ct);
                await context.Response.Body.FlushAsync(ct);
            }
        }
        var tail = sse.Complete();
        if (tail.Length > 0)
        {
            await context.Response.WriteAsync(tail, ct);
        }
        await context.Response.Body.FlushAsync(ct);
    }

    /// <summary>Unverändertes Durchreichen für alle anderen <c>/v1/*</c>-Pfade.</summary>
    public async Task PassthroughAsync(HttpContext context)
    {
        var ct = context.RequestAborted;
        var client = _httpFactory.CreateClient("upstream");

        using var upstreamRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), _options.Upstream + context.Request.Path + context.Request.QueryString);
        CopyRequestHeaders(context, upstreamRequest);

        if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            upstreamRequest.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentType is { } ctype)
            {
                upstreamRequest.Content.Headers.TryAddWithoutValidation("Content-Type", ctype);
            }
        }

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(new { error = new { type = "gateway_error", message = ex.Message } }, ct);
            return;
        }

        using (upstreamResponse)
        {
            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            CopyResponseHeaders(upstreamResponse, context);
            await using var raw = await upstreamResponse.Content.ReadAsStreamAsync(ct);
            await raw.CopyToAsync(context.Response.Body, ct);
            await context.Response.Body.FlushAsync(ct);
        }
    }

    private async Task<IReadOnlyCollection<LlmEntity>?> TryLlmFindingsAsync(string requestBody, CancellationToken ct)
    {
        try
        {
            var texts = AnthropicRewriter.ExtractRequestTexts(requestBody);
            if (texts.Count == 0)
            {
                return null;
            }
            var combined = string.Join("\n\n", texts);
            return await _llm.DetectPiiAsync(combined, model: null, ct: ct);
        }
        catch (Exception ex)
        {
            // LLM ist nur eine Ergänzung – bei Problemen ohne LLM weitermachen.
            _logger.LogDebug(ex, "Lokales LLM nicht verfügbar – nur Muster-Erkennung.");
            return null;
        }
    }

    private static void CopyRequestHeaders(HttpContext context, HttpRequestMessage upstreamRequest)
    {
        foreach (var header in context.Request.Headers)
        {
            if (SkipRequestHeaders.Contains(header.Key))
            {
                continue;
            }
            // Content-Type kommt über den Content; alle übrigen (x-api-key, anthropic-version …) hier.
            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            upstreamRequest.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value!);
        }
    }

    private static void CopyResponseHeaders(HttpResponseMessage upstreamResponse, HttpContext context)
    {
        foreach (var header in upstreamResponse.Headers)
        {
            if (SkipResponseHeaders.Contains(header.Key))
            {
                continue;
            }
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
        foreach (var header in upstreamResponse.Content.Headers)
        {
            if (SkipResponseHeaders.Contains(header.Key))
            {
                continue;
            }
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
    }
}
