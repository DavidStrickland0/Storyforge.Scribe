using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace StoryForge.Scribe.Services.Ai;

/// <summary>
/// One AI facade over OpenAI-compatible endpoints (OpenAI, Gemini, OpenRouter, Ollama, LM Studio)
/// and Anthropic's Messages API. Supports token streaming. Falls back to <see cref="OfflineMuse"/>
/// when no model is connected.
/// </summary>
public sealed class AiAssistant
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiSettingsStore _settingsStore;
    private readonly ILogger<AiAssistant> _logger;

    public AiAssistant(
        IHttpClientFactory httpClientFactory,
        AiSettingsStore settingsStore,
        ILogger<AiAssistant> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public AiSettings Settings => _settingsStore.Current;

    public bool IsOffline
    {
        get
        {
            var settings = _settingsStore.Current;
            return settings.IsOffline || !IsUsable(settings);
        }
    }

    public string ModeLabel
    {
        get
        {
            var settings = _settingsStore.Current;
            return IsUsable(settings) ? settings.Model : "Offline muse";
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = _settingsStore.Current;

        if (settings.IsOffline || !IsUsable(settings))
        {
            yield return OfflineMuse.Compose(request);
            yield break;
        }

        await foreach (var chunk in StreamWithSettingsAsync(settings, request, cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>Runs a tiny round-trip so the settings dialog can verify a connection.</summary>
    public async Task<string> TestAsync(AiSettings settings, CancellationToken cancellationToken = default)
    {
        var copy = settings.Clone();
        copy.Normalize();

        if (copy.IsOffline)
        {
            throw new AiException("Choose a model connection first.");
        }

        if (!IsUsable(copy))
        {
            throw new AiException("Add an API key, or point the base URL at a local model.");
        }

        var request = new AiRequest(
            AiAction.Freeform,
            "Connection test",
            "",
            "",
            "You are a connection test. Reply with exactly one word: ready.",
            "Reply with exactly one word: ready.");

        var sb = new StringBuilder();
        await foreach (var chunk in StreamWithSettingsAsync(copy, request, cancellationToken))
        {
            sb.Append(chunk);
            if (sb.Length > 300)
            {
                break;
            }
        }

        var text = sb.ToString().Trim();
        return text.Length == 0 ? "(connected, but the model returned nothing)" : text;
    }

    private bool IsUsable(AiSettings settings)
    {
        if (settings.IsOffline)
        {
            return false;
        }

        var key = _settingsStore.ResolveApiKey(settings);
        if (settings.Provider == AiProviders.Anthropic)
        {
            return !string.IsNullOrWhiteSpace(key);
        }

        return !string.IsNullOrWhiteSpace(key) || IsLocalEndpoint(settings.BaseUrl);
    }

    private static bool IsLocalEndpoint(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
        (uri.IsLoopback ||
         uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase));

    private async IAsyncEnumerable<string> StreamWithSettingsAsync(
        AiSettings settings,
        AiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (settings.Provider == AiProviders.Anthropic)
        {
            await foreach (var chunk in StreamAnthropicAsync(settings, request, cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            await foreach (var chunk in StreamOpenAiAsync(settings, request, cancellationToken))
            {
                yield return chunk;
            }
        }
    }

    private async IAsyncEnumerable<string> StreamOpenAiAsync(
        AiSettings settings,
        AiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("ai");
        var url = Combine(settings.BaseUrl, "chat/completions");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = request.System },
                new { role = "user", content = request.User }
            },
            ["temperature"] = settings.Temperature,
            ["max_tokens"] = settings.MaxTokens,
            ["stream"] = true
        };

        if (!string.IsNullOrWhiteSpace(settings.ReasoningEffort))
        {
            payload["reasoning_effort"] = settings.ReasoningEffort;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var key = _settingsStore.ResolveApiKey(settings);
        if (!string.IsNullOrWhiteSpace(key))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("AI request failed with {Status}. {Body}", (int)response.StatusCode, Truncate(body, 400));
            throw new AiException(FriendlyError(response.StatusCode, body));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            if (data == "[DONE]")
            {
                break;
            }

            if (!TryGetOpenAiDelta(data, out var delta, out var error))
            {
                if (error is not null)
                {
                    throw new AiException(error);
                }

                continue;
            }

            if (!string.IsNullOrEmpty(delta))
            {
                yield return delta;
            }
        }
    }

    private async IAsyncEnumerable<string> StreamAnthropicAsync(
        AiSettings settings,
        AiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("ai");
        var url = AnthropicUrl(settings.BaseUrl);

        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                model = settings.Model,
                max_tokens = settings.MaxTokens,
                temperature = settings.Temperature,
                system = request.System,
                messages = new object[] { new { role = "user", content = request.User } },
                stream = true
            })
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        message.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        var key = _settingsStore.ResolveApiKey(settings);
        if (!string.IsNullOrWhiteSpace(key))
        {
            message.Headers.TryAddWithoutValidation("x-api-key", key);
        }

        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Anthropic request failed with {Status}. {Body}", (int)response.StatusCode, Truncate(body, 400));
            throw new AiException(FriendlyError(response.StatusCode, body));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(data);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

                if (type == "error")
                {
                    var detail = root.TryGetProperty("error", out var errorElement) &&
                                 errorElement.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString()
                        : null;
                    throw new AiException(detail ?? "The AI provider returned an error.");
                }

                if (type == "message_stop")
                {
                    break;
                }

                if (type == "content_block_delta" &&
                    root.TryGetProperty("delta", out var deltaElement) &&
                    deltaElement.TryGetProperty("text", out var textElement))
                {
                    var text = textElement.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        yield return text;
                    }
                }
            }
        }
    }

    private static bool TryGetOpenAiDelta(string data, out string? delta, out string? error)
    {
        delta = null;
        error = null;

        try
        {
            using var document = JsonDocument.Parse(data);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var errorElement))
            {
                error = errorElement.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : "The AI provider returned an error.";
                return false;
            }

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return false;
            }

            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var deltaElement) &&
                deltaElement.TryGetProperty("content", out var contentElement))
            {
                delta = contentElement.GetString();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Combine(string baseUrl, string path) =>
        $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    private static string AnthropicUrl(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{trimmed}/messages"
            : $"{trimmed}/v1/messages";
    }

    private static string FriendlyError(HttpStatusCode status, string body)
    {
        var detail = "";
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                detail = error.ValueKind == JsonValueKind.Object &&
                         error.TryGetProperty("message", out var message)
                    ? message.GetString() ?? ""
                    : error.ToString();
            }
        }
        catch (JsonException)
        {
            detail = Truncate(body, 200);
        }

        var hint = status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "The API key was rejected. Check it in AI settings.",
            HttpStatusCode.NotFound =>
                "The endpoint or model was not found. Check the base URL and model name.",
            HttpStatusCode.TooManyRequests =>
                "The provider is rate limiting requests. Wait a moment and try again.",
            HttpStatusCode.BadRequest =>
                "The provider rejected the request. The model name or parameters may be wrong.",
            _ when (int)status >= 500 =>
                "The provider had a server error. Try again in a moment.",
            _ => $"The AI request failed ({(int)status})."
        };

        return string.IsNullOrWhiteSpace(detail) ? hint : $"{hint} {Truncate(detail, 280)}";
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..max] + "…";
}
