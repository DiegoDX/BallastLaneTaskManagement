using Application.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Rag.Embeddings;

public sealed class EmbeddingClientFactory : IEmbeddingClient
{
    private readonly IEmbeddingClient _inner;

    public EmbeddingClientFactory(
        IOptions<LlmSettings> settings,
        OpenAiEmbeddingClient openAiClient,
        OllamaEmbeddingClient ollamaClient)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _inner = settings.Value.Provider.Trim() switch
        {
            var provider when string.Equals(provider, LlmSettings.OpenAiProvider, StringComparison.OrdinalIgnoreCase)
                => openAiClient,
            var provider when string.Equals(provider, LlmSettings.OllamaProvider, StringComparison.OrdinalIgnoreCase)
                => ollamaClient,
            _ => throw new InvalidOperationException(
                $"Llm:Provider '{settings.Value.Provider}' is not supported.")
        };
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
        _inner.EmbedAsync(text, cancellationToken);

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default) =>
        _inner.EmbedBatchAsync(texts, cancellationToken);
}
