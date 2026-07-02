namespace Application.DTOs.Tasks;

public sealed record TaskSuggestionTaskOverride(
    string? Title,
    string? Description,
    DateTime? DueDate);
