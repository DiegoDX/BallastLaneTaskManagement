using Application.DTOs.Tasks;
using Domain.Entities;

namespace Application.Mappings;

internal static class TaskMapper
{
    internal static TaskResponse ToResponse(TaskItem task)
    {
        return new TaskResponse(
            task.Id,
            task.UserId,
            task.Title.Value,
            task.Description,
            task.Status.ToString(),
            task.DueDate.Value,
            task.CreatedAtUtc);
    }

    internal static TaskListItemResponse ToListItemResponse(TaskItem task)
    {
        return new TaskListItemResponse(
            task.Id,
            task.UserId,
            task.Title.Value,
            task.Description,
            task.Status.ToString(),
            task.DueDate.Value,
            task.CreatedAtUtc);
    }
}
