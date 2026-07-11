using Application.Rag;

namespace Application.Interfaces;

public interface IVectorStore
{
    Task UpsertAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default);
}
