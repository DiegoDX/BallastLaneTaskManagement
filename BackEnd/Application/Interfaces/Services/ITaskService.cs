using Application.DTOs.Tasks;
using Application.DTOs.Common;

namespace Application.Interfaces.Services;

public interface ITaskService
{
    Task<TaskResponse> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);

    Task UpdateTaskAsync(UpdateTaskRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<TaskResponse> GetTaskByIdAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    Task<PagedResult<TaskListItemResponse>> SearchTasksAsync(
        Guid userId,
        TaskSearchRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
}
