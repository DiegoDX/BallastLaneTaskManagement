namespace Application.DTOs.DocAssistant;

public sealed record DocAssistantRequest(IReadOnlyList<DocAssistantMessageDto> Messages);
