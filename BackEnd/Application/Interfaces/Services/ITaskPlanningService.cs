using Application.DTOs.Tasks;

namespace Application.Interfaces.Services;

public interface ITaskPlanningService
{
    Task<StudyPlanResponse> GenerateStudyPlanAsync(
        Guid userId,
        string topic,
        string? dueDate,
        bool createTasks,
        CancellationToken cancellationToken = default);

    Task<PrioritizeTasksResponse> PrioritizeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SummarizeProgressResponse> SummarizeProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SuggestNextTaskResponse> SuggestNextAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
