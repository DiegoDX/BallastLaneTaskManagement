namespace Application.DTOs.TaskAssistant;

public sealed record TaskAssistantRequest(IReadOnlyList<TaskAssistantMessageDto> Messages);
