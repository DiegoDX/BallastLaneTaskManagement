namespace Application.DTOs.TaskAssistant;

public sealed record TaskAssistantResponse(
    string Content,
    IReadOnlyList<TaskAssistantAction> Actions,
    string? Model = null);
