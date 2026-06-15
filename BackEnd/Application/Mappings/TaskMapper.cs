using Application.DTOs.Tasks;
using Domain.Entities;

namespace Application.Mappings;

internal static class TaskMapper
{
    internal static TaskResponse ToResponse(TaskItem task)
    {
        return new TaskResponse(task.Id, task.UserId, task.Title.Value, task.Description, task.Status.ToString(), task.DueDate.Value);

    }
}
