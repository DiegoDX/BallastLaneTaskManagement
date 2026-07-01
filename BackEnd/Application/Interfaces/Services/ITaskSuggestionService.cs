using Application.DTOs.Tasks;

namespace Application.Interfaces.Services;

public interface ITaskSuggestionService
{
    Task<TaskSuggestionResponse> SuggestAsync(
        Guid userId,
        TaskSuggestionRequest request,
        CancellationToken cancellationToken = default);
}
