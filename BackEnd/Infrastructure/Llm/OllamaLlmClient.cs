using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.Llm.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Llm;

public sealed class OllamaLlmClient : ILlmClient
{
    private const string DefaultModel = "llama3.2";
    private const string DefaultBaseUrl = "http://localhost:11434";
    private const string ChatEndpointPath = "/api/chat";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly LlmSettings _settings;
    private readonly ILogger<OllamaLlmClient> _logger;

    public OllamaLlmClient(
        HttpClient httpClient,
        IOptions<LlmSettings> settings,
        ILogger<OllamaLlmClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureAuthorization(_httpClient, _settings);
    }

    public async Task<LlmChatResponse> CompleteChatAsync(
        LlmChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = ResolveModel(request);
        var ollamaRequest = OllamaChatRequestMapper.ToOllamaRequest(request, model);
        var chatEndpoint = BuildChatEndpoint();
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
                    "LLM request timed out before a response was received.",
                    isTransient: true);
            }

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(remaining);

            try
            {
                _logger.LogDebug(
                    "Sending Ollama chat completion request. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Model={Model}, RemainingTimeoutMs={RemainingTimeoutMs}",
                    attempt,
                    maxAttempts,
                    model,
                    (int)remaining.TotalMilliseconds);

                using var httpResponse = await _httpClient.PostAsJsonAsync(
                    chatEndpoint,
                    ollamaRequest,
                    JsonOptions,
                    attemptCts.Token);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var isTransient = IsTransientStatusCode(httpResponse.StatusCode);

                    if (isTransient && attempt < maxAttempts)
                    {
                        var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));

                        _logger.LogWarning(
                            "Transient Ollama chat completion failure. Attempt={Attempt}, MaxAttempts={MaxAttempts}, StatusCode={StatusCode}, RetryDelayMs={RetryDelayMs}",
                            attempt,
                            maxAttempts,
                            (int)httpResponse.StatusCode,
                            delay.TotalMilliseconds);

                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    stopwatch.Stop();

                    _logger.LogError(
                        "Ollama chat completion failed. Attempt={Attempt}, MaxAttempts={MaxAttempts}, StatusCode={StatusCode}, Transient={IsTransient}, LatencyMs={LatencyMs}",
                        attempt,
                        maxAttempts,
                        (int)httpResponse.StatusCode,
                        isTransient,
                        stopwatch.ElapsedMilliseconds);

                    throw new LlmException(
                        "Failed to complete chat with the LLM provider.",
                        isTransient);
                }

                var ollamaResponse = await httpResponse.Content.ReadFromJsonAsync<OllamaChatResponseDto>(
                    JsonOptions,
                    attemptCts.Token);

                if (ollamaResponse is null)
                {
                    stopwatch.Stop();
                    throw new LlmException(
                        "Failed to complete chat with the LLM provider.",
                        isTransient: false);
                }

                stopwatch.Stop();

                var response = OllamaChatResponseMapper.ToLlmChatResponse(ollamaResponse, model);

                _logger.LogInformation(
                    "Ollama chat completion succeeded. Provider={Provider}, Model={Model}, LatencyMs={LatencyMs}",
                    LlmSettings.OllamaProvider,
                    response.Model,
                    stopwatch.ElapsedMilliseconds);

                _logger.LogDebug(
                    "Ollama chat completion response received. ContentLength={ContentLength}",
                    response.Content.Length);

                return response;
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken) && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));

                _logger.LogWarning(
                    ex,
                    "Transient Ollama chat completion failure. Attempt={Attempt}, MaxAttempts={MaxAttempts}, RetryDelayMs={RetryDelayMs}",
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
                    "Ollama chat completion failed. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Transient={IsTransient}, LatencyMs={LatencyMs}",
                    attempt,
                    maxAttempts,
                    isTransient,
                    stopwatch.ElapsedMilliseconds);

                throw new LlmException(
                    "Failed to complete chat with the LLM provider.",
                    ex,
                    isTransient);
            }
        }

        throw new LlmException(
            "Failed to complete chat with the LLM provider.",
            isTransient: true);
    }

    private string ResolveModel(LlmChatRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            return request.Model;
        }

        if (!string.IsNullOrWhiteSpace(_settings.Model))
        {
            return _settings.Model;
        }

        return DefaultModel;
    }

    private string BuildChatEndpoint()
    {
        var baseUrl = ResolveBaseUrl().TrimEnd('/');
        return $"{baseUrl}{ChatEndpointPath}";
    }

    private string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            return _settings.BaseUrl;
        }

        return DefaultBaseUrl;
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
