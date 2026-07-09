namespace Application.DTOs.Tasks;

public sealed record TaskSuggestionCreateRequest(
    IReadOnlyList<TaskSuggestionBatchItem>? Tasks);
