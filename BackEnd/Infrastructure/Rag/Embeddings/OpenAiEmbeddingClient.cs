using System.ClientModel;
using System.Diagnostics;
using Application.Exceptions;
using Application.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace Infrastructure.Rag.Embeddings;

public sealed class OpenAiEmbeddingClient : IEmbeddingClient
{
    private readonly LlmSettings _settings;
    private readonly ILogger<OpenAiEmbeddingClient> _logger;
    private readonly OpenAIClient _openAiClient;

    public OpenAiEmbeddingClient(IOptions<LlmSettings> settings, ILogger<OpenAiEmbeddingClient> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _openAiClient = CreateOpenAiClient(_settings);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await EmbedInternalAsync([text], cancellationToken);
        return embeddings[0];
    }

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default) =>
        EmbedInternalAsync(texts, cancellationToken);

    private async Task<IReadOnlyList<float[]>> EmbedInternalAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        if (texts.Any(string.IsNullOrWhiteSpace))
        {
            throw new LlmException("Embedding text cannot be empty.", isTransient: false);
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new LlmException("LLM API key is not configured.", isTransient: false);
        }

        var model = ResolveEmbeddingModel();
        var embeddingClient = _openAiClient.GetEmbeddingClient(model);
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
                    "Sending OpenAI embedding request. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Model={Model}, TextCount={TextCount}, RemainingTimeoutMs={RemainingTimeoutMs}",
                    attempt,
                    maxAttempts,
                    model,
                    texts.Count,
                    (int)remaining.TotalMilliseconds);

                OpenAIEmbeddingCollection embeddingCollection;

                if (texts.Count == 1)
                {
                    var singleResult = await embeddingClient.GenerateEmbeddingAsync(
                        texts[0],
                        cancellationToken: attemptCts.Token);

                    embeddingCollection = OpenAIEmbeddingsModelFactory.OpenAIEmbeddingCollection(
                        [singleResult.Value],
                        model,
                        null);
                }
                else
                {
                    var batchResult = await embeddingClient.GenerateEmbeddingsAsync(
                        texts,
                        cancellationToken: attemptCts.Token);

                    embeddingCollection = batchResult.Value;
                }

                stopwatch.Stop();

                var embeddings = embeddingCollection
                    .Select(embedding => embedding.ToFloats().ToArray())
                    .ToArray();

                if (embeddings.Length != texts.Count || embeddings.Any(vector => vector.Length == 0))
                {
                    throw new LlmException(
                        "Failed to generate embeddings with the LLM provider.",
                        isTransient: false);
                }

                _logger.LogInformation(
                    "OpenAI embedding succeeded. Provider={Provider}, Model={Model}, TextCount={TextCount}, Dimensions={Dimensions}, LatencyMs={LatencyMs}",
                    LlmSettings.OpenAiProvider,
                    model,
                    texts.Count,
                    embeddings[0].Length,
                    stopwatch.ElapsedMilliseconds);

                return embeddings;
            }
            catch (Exception ex) when (IsTransientFailure(ex, cancellationToken) && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));

                _logger.LogWarning(
                    ex,
                    "Transient OpenAI embedding failure. Attempt={Attempt}, MaxAttempts={MaxAttempts}, RetryDelayMs={RetryDelayMs}",
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
                    "OpenAI embedding failed. Attempt={Attempt}, MaxAttempts={MaxAttempts}, Transient={IsTransient}, LatencyMs={LatencyMs}",
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

        return LlmSettings.DefaultOpenAiEmbeddingModel;
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
