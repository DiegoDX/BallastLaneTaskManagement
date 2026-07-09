namespace Application.DTOs.Tasks;

public sealed record TaskSuggestionBatchResponse(IReadOnlyList<TaskSuggestionBatchItem> Tasks);
