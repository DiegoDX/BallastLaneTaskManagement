namespace Application.Rag;

public sealed record DocumentChunk(
    string Id,
    string SourceFile,
    int ChunkIndex,
    string Content,
    float[] Embedding);
