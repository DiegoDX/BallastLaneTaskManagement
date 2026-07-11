using Application.Interfaces;

namespace Application.Rag;

public sealed class RagRetriever : IRagRetriever
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;

    public RagRetriever(IEmbeddingClient embeddingClient, IVectorStore vectorStore)
    {
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    public async Task<IReadOnlyList<DocumentChunk>> RetrieveAsync(
        string question,
        int topK,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var queryEmbedding = await _embeddingClient.EmbedAsync(question, cancellationToken);
        return await _vectorStore.SearchAsync(queryEmbedding, topK, cancellationToken);
    }
}
