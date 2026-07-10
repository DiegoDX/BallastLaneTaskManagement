using System.ClientModel;
using System.Diagnostics;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.Llm.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Infrastructure.Llm;

public sealed class OpenAiLlmClient : ILlmClient
{
    private readonly LlmSettings _settings;
    private readonly ILogger<OpenAiLlmClient> _logger;
    private readonly OpenAIClient _openAiClient;

    public OpenAiLlmClient(IOptions<LlmSettings> settings, ILogger<OpenAiLlmClient> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _openAiClient = CreateOpenAiClient(_settings);
    }

    public async Task<LlmChatResponse> CompleteChatAsync(
        LlmChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new LlmException("LLM API key is not configured.", isTransient: false);
        }

        var messages = OpenAiChatMessageMapper.ToOpenAiMessages(request.Messages);
        var options = OpenAiChatCompletionOptionsMapper.ToOpenAiOptions(request);
        var model = ResolveModel(request);
        var chatClient = _openAiClient.GetChatClient(model);
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
                    "Sending OpenAI chat completion request. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Model={Model}, RemainingTimeoutMs={RemainingTimeoutMs}",
                    attempt,
                    maxAttempts,
                    model,
                    (int)remaining.TotalMilliseconds);

                var completion = await chatClient.CompleteChatAsync(
                    messages,
                    options,
                    attemptCts.Token);

                stopwatch.Stop();

                var response = OpenAiChatResponseMapper.ToLlmChatResponse(completion, model);

                _logger.LogInformation(
                    "OpenAI chat completion succeeded. Provider={Provider}, Model={Model}, LatencyMs={LatencyMs}",
                    LlmSettings.OpenAiProvider,
                    response.Model,
                    stopwatch.ElapsedMilliseconds);

                _logger.LogDebug(
                    "OpenAI chat completion response received. ContentLength={ContentLength}",
                    response.Content.Length);

                return response;
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken) && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));

                _logger.LogWarning(
                    ex,
                    "Transient OpenAI chat completion failure. Attempt={Attempt}, MaxAttempts={MaxAttempts}, RetryDelayMs={RetryDelayMs}",
                    attempt,
                    maxAttempts,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var isTransient = IsTransientFailure(ex, cancellationToken);

                _logger.LogError(
                    ex,
                    "OpenAI chat completion failed. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Transient={IsTransient}, LatencyMs={LatencyMs}",
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

    public async Task<LlmChatCompletion> CompleteChatWithToolsAsync(
        LlmChatRequest request,
        IReadOnlyList<LlmToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(tools);

        if (tools.Count == 0)
        {
            throw new LlmException("At least one tool is required.", isTransient: false);
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new LlmException("LLM API key is not configured.", isTransient: false);
        }

        var messages = OpenAiChatMessageMapper.ToOpenAiMessagesWithTools(request.Messages);
        var options = OpenAiChatCompletionOptionsMapper.ToOpenAiOptionsWithTools(request, tools);
        var model = ResolveModel(request);
        var chatClient = _openAiClient.GetChatClient(model);
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
                    "Sending OpenAI chat completion request with tools. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Model={Model}, ToolCount={ToolCount}, RemainingTimeoutMs={RemainingTimeoutMs}",
                    attempt,
                    maxAttempts,
                    model,
                    tools.Count,
                    (int)remaining.TotalMilliseconds);

                var completion = await chatClient.CompleteChatAsync(
                    messages,
                    options,
                    attemptCts.Token);

                stopwatch.Stop();

                var response = OpenAiChatCompletionMapper.ToLlmChatCompletion(completion, model);

                _logger.LogInformation(
                    "OpenAI chat completion with tools succeeded. Provider={Provider}, Model={Model}, ToolCallCount={ToolCallCount}, LatencyMs={LatencyMs}",
                    LlmSettings.OpenAiProvider,
                    response.Model,
                    response.ToolCalls.Count,
                    stopwatch.ElapsedMilliseconds);

                _logger.LogDebug(
                    "OpenAI chat completion with tools response received. ContentLength={ContentLength}, ToolCallCount={ToolCallCount}",
                    response.Content.Length,
                    response.ToolCalls.Count);

                return response;
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken) && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));

                _logger.LogWarning(
                    ex,
                    "Transient OpenAI chat completion with tools failure. Attempt={Attempt}, MaxAttempts={MaxAttempts}, RetryDelayMs={RetryDelayMs}",
                    attempt,
                    maxAttempts,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var isTransient = IsTransientFailure(ex, cancellationToken);

                _logger.LogError(
                    ex,
                    "OpenAI chat completion with tools failed. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Transient={IsTransient}, LatencyMs={LatencyMs}",
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

        return "gpt-4o-mini";
    }

    private static OpenAIClient CreateOpenAiClient(LlmSettings settings)
    {
        var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? "unset" : settings.ApiKey;

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return new OpenAIClient(apiKey);
        }

        var credential = new ApiKeyCredential(apiKey);
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(settings.BaseUrl)
        };

        return new OpenAIClient(credential, clientOptions);
    }

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

        if (exception is ClientResultException clientResultException)
        {
            return clientResultException.Status is 429 or 503;
        }

        return false;
    }
}
