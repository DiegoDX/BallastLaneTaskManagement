using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Exceptions;
using Application.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Rag.Embeddings;

public sealed class OllamaEmbeddingClient : IEmbeddingClient
{
    private const string EmbeddingsEndpointPath = "/api/embeddings";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly LlmSettings _settings;
    private readonly ILogger<OllamaEmbeddingClient> _logger;

    public OllamaEmbeddingClient(
        HttpClient httpClient,
        IOptions<LlmSettings> settings,
        ILogger<OllamaEmbeddingClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureAuthorization(_httpClient, _settings);
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
        EmbedInternalAsync(text, cancellationToken);

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        var embeddings = new List<float[]>(texts.Count);

        foreach (var text in texts)
        {
            embeddings.Add(await EmbedInternalAsync(text, cancellationToken));
        }

        return embeddings;
    }

    private async Task<float[]> EmbedInternalAsync(
        string text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new LlmException("Embedding text cannot be empty.", isTransient: false);
        }

        var model = ResolveEmbeddingModel();
        var embeddingsEndpoint = BuildEmbeddingsEndpoint();
        var request = new OllamaEmbeddingRequestDto
        {
            Model = model,
            Prompt = text
        };

        var maxAttempts = _settings.MaxRetryAttempts + 1;
        var stopwatch = Stopwatch.StartNew();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(_settings.TimeoutSeconds);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                stopwatch.Stop();
                throw new LlmException(
                    "Embedding request timed out before a response was received.",
                    isTransient: true);
            }

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(remaining);

            try
            {
                _logger.LogDebug(
                    "Sending Ollama embedding request. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Model={Model}, RemainingTimeoutMs={RemainingTimeoutMs}",
                    attempt,
                    maxAttempts,
                    model,
                    (int)remaining.TotalMilliseconds);

                using var httpResponse = await _httpClient.PostAsJsonAsync(
                    embeddingsEndpoint,
                    request,
                    JsonOptions,
                    attemptCts.Token);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var isTransient = IsTransientStatusCode(httpResponse.StatusCode);

                    if (isTransient && attempt < maxAttempts)
                    {
                        var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));

                        _logger.LogWarning(
                            "Transient Ollama embedding failure. Attempt={Attempt}, MaxAttempts={MaxAttempts}, StatusCode={StatusCode}, RetryDelayMs={RetryDelayMs}",
                            attempt,
                            maxAttempts,
                            (int)httpResponse.StatusCode,
                            delay.TotalMilliseconds);

                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    stopwatch.Stop();

                    _logger.LogError(
                        "Ollama embedding failed. Attempt={Attempt}, MaxAttempts={MaxAttempts}, StatusCode={StatusCode}, Transient={IsTransient}, LatencyMs={LatencyMs}",
                        attempt,
                        maxAttempts,
                        (int)httpResponse.StatusCode,
                        isTransient,
                        stopwatch.ElapsedMilliseconds);

                    throw new LlmException(
                        "Failed to generate embeddings with the LLM provider.",
                        isTransient);
                }

                var ollamaResponse = await httpResponse.Content.ReadFromJsonAsync<OllamaEmbeddingResponseDto>(
                    JsonOptions,
                    attemptCts.Token);

                if (ollamaResponse?.Embedding is not { Length: > 0 })
                {
                    stopwatch.Stop();
                    throw new LlmException(
                        "Failed to generate embeddings with the LLM provider.",
                        isTransient: false);
                }

                stopwatch.Stop();

                _logger.LogInformation(
                    "Ollama embedding succeeded. Provider={Provider}, Model={Model}, Dimensions={Dimensions}, LatencyMs={LatencyMs}",
                    LlmSettings.OllamaProvider,
                    model,
                    ollamaResponse.Embedding.Length,
                    stopwatch.ElapsedMilliseconds);

                return ollamaResponse.Embedding;
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken) && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));

                _logger.LogWarning(
                    ex,
                    "Transient Ollama embedding failure. Attempt={Attempt}, MaxAttempts={MaxAttempts}, RetryDelayMs={RetryDelayMs}",
                    attempt,
                    maxAttempts,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (LlmException)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var isTransient = IsTransientFailure(ex, cancellationToken);

                _logger.LogError(
                    ex,
                    "Ollama embedding failed. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Transient={IsTransient}, LatencyMs={LatencyMs}",
                    attempt,
                    maxAttempts,
                    isTransient,
                    stopwatch.ElapsedMilliseconds);

                throw new LlmException(
                    "Failed to generate embeddings with the LLM provider.",
                    ex,
                    isTransient);
            }
        }

        throw new LlmException(
            "Failed to generate embeddings with the LLM provider.",
            isTransient: true);
    }

    private string ResolveEmbeddingModel()
    {
        if (!string.IsNullOrWhiteSpace(_settings.EmbeddingModel))
        {
            return _settings.EmbeddingModel;
        }

        return LlmSettings.DefaultOllamaEmbeddingModel;
    }

    private string BuildEmbeddingsEndpoint()
    {
        var baseUrl = ResolveBaseUrl().TrimEnd('/');
        return $"{baseUrl}{EmbeddingsEndpointPath}";
    }

    private string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            return _settings.BaseUrl;
        }

        return "http://localhost:11434";
    }

    private static void ConfigureAuthorization(HttpClient httpClient, LlmSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return;
        }

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.ApiKey);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;

    private static bool IsTransientFailure(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested &&
            exception is OperationCanceledException)
        {
            return false;
        }

        if (exception is OperationCanceledException or TaskCanceledException)
        {
            return true;
        }

        if (exception is HttpRequestException)
        {
            return true;
        }

        return false;
    }
}
