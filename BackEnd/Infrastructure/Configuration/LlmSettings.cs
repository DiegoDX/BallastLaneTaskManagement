namespace Infrastructure.Configuration;

public sealed class LlmSettings
{
    public const string SectionName = "Llm";

    public const string OpenAiProvider = "OpenAI";

    public const string OllamaProvider = "Ollama";

    public string Provider { get; init; } = OpenAiProvider;

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "gpt-4o-mini";

    public const string DefaultOpenAiEmbeddingModel = "text-embedding-3-small";

    public const string DefaultOllamaEmbeddingModel = "nomic-embed-text";

    public string EmbeddingModel { get; init; } = string.Empty;

    public string? BaseUrl { get; init; }

    public int TimeoutSeconds { get; init; } = 60;

    public int MaxRetryAttempts { get; init; } = 2;
}
