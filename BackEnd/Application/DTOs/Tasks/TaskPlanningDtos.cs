namespace Application.DTOs.Tasks;

public sealed record TaskPlanningItem(
    Guid TaskId,
    string Title,
    string Status,
    DateTime DueDate,
    int PriorityRank);

public sealed record StudyPlanResponse(
    string Goal,
    IReadOnlyList<string> Steps,
    IReadOnlyList<TaskPlanningItem> RelatedTasks);

public sealed record PrioritizeTasksResponse(
    IReadOnlyList<TaskPlanningItem> Tasks);

public sealed record SummarizeProgressResponse(
    string Summary,
    TaskStatisticsResponse Statistics);

public sealed record SuggestNextTaskResponse(
    Guid? TaskId,
    string? Title,
    string? Status,
    DateTime? DueDate,
    string Reason);
