using Application.Rag;

namespace Application.Interfaces;

public interface IRagRetriever
{
    Task<IReadOnlyList<DocumentChunk>> RetrieveAsync(
        string question,
        int topK,
        CancellationToken cancellationToken = default);
}
