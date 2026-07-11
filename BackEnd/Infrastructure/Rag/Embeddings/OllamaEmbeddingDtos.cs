using System.Text.Json.Serialization;

namespace Infrastructure.Rag.Embeddings;

internal sealed class OllamaEmbeddingRequestDto
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }
}

internal sealed class OllamaEmbeddingResponseDto
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; init; } = [];
}
