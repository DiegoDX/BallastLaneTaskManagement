namespace Application.DTOs.DocAssistant;

public sealed record DocAssistantSource(string FileName, int ChunkIndex, string Excerpt);
