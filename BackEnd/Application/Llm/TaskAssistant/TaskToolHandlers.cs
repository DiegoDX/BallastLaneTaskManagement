using System.Text.Json;
using Application.DTOs.Common;
using Application.DTOs.TaskAssistant;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces.Services;
using Domain.ValueObjects;

namespace Application.Llm.TaskAssistant;

public sealed class TaskToolHandlers : ITaskToolHandlers
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    private readonly ITaskService _taskService;
    private readonly TimeProvider _timeProvider;

    public TaskToolHandlers(ITaskService taskService, TimeProvider timeProvider)
    {
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<TaskToolExecutionResult> CreateTaskAsync(
        Guid userId,
        string? title,
        string? description,
        string? dueDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return ErrorResult("Title is required.");
        }

        var normalizedTitle = title.Trim();

        if (normalizedTitle.Length > TaskTitle.MaxLength)
        {
            return ErrorResult($"Title must be at most {TaskTitle.MaxLength} characters long.");
        }

        var parsedDueDate = NaturalDueDateParser.TryParse(dueDate, _timeProvider);

        if (parsedDueDate is null)
        {
            return ErrorResult("A valid dueDate in ISO format (YYYY-MM-DD) is required.");
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : description.Trim();

        try
        {
            var createRequest = new CreateTaskRequest(
                normalizedTitle,
                normalizedDescription,
                parsedDueDate.Value,
                userId);

            var created = await _taskService.CreateTaskAsync(createRequest, cancellationToken);

            var action = new TaskAssistantAction(
                TaskAssistantActionTypes.Created,
                created.Id,
                created.Title,
                DueDate: created.DueDate);

            return SuccessResult(created, action);
        }
        catch (ValidationException ex)
        {
            return ErrorResult(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return ErrorResult(ex.Message);
        }
    }

    public async Task<TaskToolExecutionResult> SearchTasksAsync(
        Guid userId,
        string? taskId,
        string? status,
        string? title,
        int? pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            if (!TryParseGuid(taskId, out var parsedTaskId, out var parseError))
            {
                return ErrorResult(parseError);
            }

            try
            {
                var task = await _taskService.GetTaskByIdAsync(parsedTaskId, userId, cancellationToken);

                var payload = new
                {
                    success = true,
                    totalRecords = 1,
                    pageNumber = 1,
                    pageSize = 1,
                    tasks = new[]
                    {
                        new
                        {
                            taskId = task.Id,
                            title = task.Title,
                            description = task.Description,
                            status = task.Status,
                            dueDate = task.DueDate,
                            createdDate = task.CreatedDate
                        }
                    }
                };

                return new TaskToolExecutionResult(
                    JsonSerializer.Serialize(payload),
                    new TaskAssistantAction(TaskAssistantActionTypes.Listed));
            }
            catch (ValidationException ex)
            {
                return ErrorResult(ex.Message);
            }
            catch (NotFoundException ex)
            {
                return ErrorResult(ex.Message);
            }
        }

        var normalizedPageSize = pageSize ?? DefaultPageSize;

        if (normalizedPageSize <= 0 || normalizedPageSize > MaxPageSize)
        {
            return ErrorResult($"pageSize must be between 1 and {MaxPageSize}.");
        }

        var searchRequest = new TaskSearchRequest
        {
            PageNumber = 1,
            PageSize = normalizedPageSize,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim()
        };

        try
        {
            var result = await _taskService.SearchTasksAsync(userId, searchRequest, cancellationToken);

            return new TaskToolExecutionResult(
                JsonSerializer.Serialize(BuildListTasksPayload(result)),
                new TaskAssistantAction(TaskAssistantActionTypes.Listed));
        }
        catch (ValidationException ex)
        {
            return ErrorResult(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return ErrorResult(ex.Message);
        }
    }

    public async Task<TaskToolExecutionResult> UpdateTaskAsync(
        Guid userId,
        string? taskId,
        string? title,
        string? description,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseGuid(taskId, out var parsedTaskId, out var taskIdError))
        {
            return ErrorResult(taskIdError);
        }

        if (title is null && description is null && status is null)
        {
            return ErrorResult("At least one of title, description, or status must be provided.");
        }

        TaskResponse existing;

        try
        {
            existing = await _taskService.GetTaskByIdAsync(parsedTaskId, userId, cancellationToken);
        }
        catch (ValidationException ex)
        {
            return ErrorResult(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return ErrorResult(ex.Message);
        }

        var normalizedTitle = title is null ? existing.Title : title.Trim();

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return ErrorResult("Title is required.");
        }

        if (normalizedTitle.Length > TaskTitle.MaxLength)
        {
            return ErrorResult($"Title must be at most {TaskTitle.MaxLength} characters long.");
        }

        var normalizedDescription = description is null ? existing.Description : description.Trim();
        var normalizedStatus = status is null ? existing.Status : status.Trim();

        try
        {
            var updateRequest = new UpdateTaskRequest(
                parsedTaskId,
                normalizedTitle,
                normalizedDescription,
                normalizedStatus);

            await _taskService.UpdateTaskAsync(updateRequest, userId, cancellationToken);

            var updated = await _taskService.GetTaskByIdAsync(parsedTaskId, userId, cancellationToken);
            var action = new TaskAssistantAction(
                TaskAssistantActionTypes.Updated,
                updated.Id,
                updated.Title,
                Status: updated.Status);

            var payload = new
            {
                success = true,
                taskId = updated.Id,
                title = updated.Title,
                description = updated.Description,
                status = updated.Status,
                dueDate = updated.DueDate
            };

            return new TaskToolExecutionResult(JsonSerializer.Serialize(payload), action);
        }
        catch (ValidationException ex)
        {
            return ErrorResult(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return ErrorResult(ex.Message);
        }
    }

    public async Task<TaskToolExecutionResult> DeleteTaskAsync(
        Guid userId,
        string? taskId,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseGuid(taskId, out var parsedTaskId, out var parseError))
        {
            return ErrorResult(parseError);
        }

        TaskResponse existing;

        try
        {
            existing = await _taskService.GetTaskByIdAsync(parsedTaskId, userId, cancellationToken);
        }
        catch (ValidationException ex)
        {
            return ErrorResult(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return ErrorResult(ex.Message);
        }

        try
        {
            await _taskService.DeleteTaskAsync(parsedTaskId, userId, cancellationToken);

            var action = new TaskAssistantAction(
                TaskAssistantActionTypes.Deleted,
                existing.Id,
                existing.Title);

            var payload = new
            {
                success = true,
                taskId = existing.Id,
                title = existing.Title
            };

            return new TaskToolExecutionResult(JsonSerializer.Serialize(payload), action);
        }
        catch (ValidationException ex)
        {
            return ErrorResult(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return ErrorResult(ex.Message);
        }
    }

    public Task<TaskToolExecutionResult> CompleteTaskAsync(
        Guid userId,
        string? taskId,
        CancellationToken cancellationToken = default) =>
        UpdateTaskAsync(userId, taskId, null, null, "Completed", cancellationToken);

    internal static bool TryParseGuid(string? value, out Guid taskId, out string error)
    {
        taskId = Guid.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "taskId is required.";
            return false;
        }

        if (!Guid.TryParse(value.Trim(), out taskId) || taskId == Guid.Empty)
        {
            error = "taskId must be a valid GUID.";
            return false;
        }

        return true;
    }

    private static object BuildListTasksPayload(PagedResult<TaskListItemResponse> result) =>
        new
        {
            success = true,
            totalRecords = result.TotalRecords,
            pageNumber = result.PageNumber,
            pageSize = result.PageSize,
            tasks = result.Items.Select(task => new
            {
                taskId = task.Id,
                title = task.Title,
                description = task.Description,
                status = task.Status,
                dueDate = task.DueDate,
                createdDate = task.CreatedDate
            })
        };

    private static TaskToolExecutionResult SuccessResult(TaskResponse created, TaskAssistantAction action)
    {
        var payload = new
        {
            success = true,
            taskId = created.Id,
            title = created.Title,
            dueDate = created.DueDate
        };

        return new TaskToolExecutionResult(JsonSerializer.Serialize(payload), action);
    }

    private static TaskToolExecutionResult ErrorResult(string message)
    {
        var payload = new { success = false, error = message };
        return new TaskToolExecutionResult(JsonSerializer.Serialize(payload));
    }
}
