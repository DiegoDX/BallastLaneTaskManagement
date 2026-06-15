using Application.DTOs.Tasks;

namespace Application.Interfaces.Services;

public interface ITaskService
{
    Task<TaskResponse> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);

    Task UpdateTaskAsync(UpdateTaskRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<TaskResponse> GetTaskByIdAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskResponse>> GetTasksByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task DeleteTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
}
