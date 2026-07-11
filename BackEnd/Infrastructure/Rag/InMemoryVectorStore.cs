using Application.Interfaces;
using Application.Rag;

namespace Infrastructure.Rag;

public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly object _lock = new();
    private List<DocumentChunk> _chunks = [];

    public Task UpsertAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _chunks = [.. chunks];
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        if (topK < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "TopK must be non-negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        List<DocumentChunk> snapshot;
        lock (_lock)
        {
            snapshot = _chunks;
        }

        if (topK == 0 || snapshot.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<DocumentChunk>>([]);
        }

        var results = snapshot
            .Select(chunk => (Chunk: chunk, Score: CosineSimilarity.Compute(queryEmbedding, chunk.Embedding)))
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .Select(result => result.Chunk)
            .ToList();

        return Task.FromResult<IReadOnlyList<DocumentChunk>>(results);
    }
}
