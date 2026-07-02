namespace Application.DTOs.Tasks;

public sealed record TaskSuggestionCreateRequest(
    string? Prompt,
    int TaskCount,
    DateTime? DueDate,
    IReadOnlyList<TaskSuggestionTaskOverride>? Tasks);
