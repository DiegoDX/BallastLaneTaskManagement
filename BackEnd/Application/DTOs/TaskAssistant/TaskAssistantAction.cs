namespace Application.DTOs.TaskAssistant;

public sealed record TaskAssistantAction(
    string Type,
    Guid? TaskId = null,
    string? Title = null,
    string? Status = null,
    DateTime? DueDate = null);
