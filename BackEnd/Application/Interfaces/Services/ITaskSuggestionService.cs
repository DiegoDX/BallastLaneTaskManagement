using Application.DTOs.Tasks;

namespace Application.Interfaces.Services;

public interface ITaskSuggestionService
{
    Task<TaskSuggestionResponse> SuggestAsync(
        Guid userId,
        TaskSuggestionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskResponse>> CreateFromSuggestionsAsync(
        Guid userId,
        TaskSuggestionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<TaskSuggestionBatchResponse> GenerateBatchAsync(
        Guid userId,
        TaskSuggestionGenerateRequest request,
        CancellationToken cancellationToken = default);
}
