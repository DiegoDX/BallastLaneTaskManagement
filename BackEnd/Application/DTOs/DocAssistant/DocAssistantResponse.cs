namespace Application.DTOs.DocAssistant;

public sealed record DocAssistantResponse(
    string Content,
    IReadOnlyList<DocAssistantSource> Sources,
    string? Model = null);
