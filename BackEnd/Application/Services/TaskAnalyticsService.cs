using Application.DTOs.Tasks;
using Application.Interfaces.Services;
using Domain.Enums;

namespace Application.Services;

public sealed class TaskAnalyticsService : ITaskAnalyticsService
{
    private readonly ITaskService _taskService;
    private readonly TimeProvider _timeProvider;

    public TaskAnalyticsService(ITaskService taskService, TimeProvider timeProvider)
    {
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<TaskStatisticsResponse> GetStatisticsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var searchRequest = new TaskSearchRequest
        {
            PageNumber = 1,
            PageSize = 100
        };

        var result = await _taskService.SearchTasksAsync(userId, searchRequest, cancellationToken);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var pending = 0;
        var inProgress = 0;
        var completed = 0;
        var overdue = 0;
        var dueToday = 0;

        foreach (var task in result.Items)
        {
            if (string.Equals(task.Status, TaskItemStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                completed++;
                continue;
            }

            if (string.Equals(task.Status, TaskItemStatus.InProgress.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                inProgress++;
            }
            else
            {
                pending++;
            }

            var dueDate = DateOnly.FromDateTime(task.DueDate.Date);

            if (dueDate < today)
            {
                overdue++;
            }
            else if (dueDate == today)
            {
                dueToday++;
            }
        }

        return new TaskStatisticsResponse(
            result.TotalRecords,
            pending,
            inProgress,
            completed,
            overdue,
            dueToday);
    }
}
