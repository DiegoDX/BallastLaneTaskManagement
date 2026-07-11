namespace Application.Llm.TaskAssistant;

public interface ITaskToolHandlers
{
    Task<TaskToolExecutionResult> CreateTaskAsync(
        Guid userId,
        string? title,
        string? description,
        string? dueDate,
        CancellationToken cancellationToken = default);

    Task<TaskToolExecutionResult> SearchTasksAsync(
        Guid userId,
        string? taskId,
        string? status,
        string? title,
        int? pageSize,
        CancellationToken cancellationToken = default);

    Task<TaskToolExecutionResult> UpdateTaskAsync(
        Guid userId,
        string? taskId,
        string? title,
        string? description,
        string? status,
        CancellationToken cancellationToken = default);

    Task<TaskToolExecutionResult> DeleteTaskAsync(
        Guid userId,
        string? taskId,
        CancellationToken cancellationToken = default);

    Task<TaskToolExecutionResult> CompleteTaskAsync(
        Guid userId,
        string? taskId,
        CancellationToken cancellationToken = default);
}
