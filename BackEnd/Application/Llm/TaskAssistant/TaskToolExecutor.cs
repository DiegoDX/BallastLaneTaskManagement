using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Common;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces.Services;
using Domain.ValueObjects;

namespace Application.Llm.TaskAssistant;

public sealed class TaskToolExecutor : ITaskToolExecutor
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ITaskService _taskService;
    private readonly TimeProvider _timeProvider;

    public TaskToolExecutor(ITaskService taskService, TimeProvider timeProvider)
    {
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<TaskToolExecutionResult> ExecuteAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        return toolCall.Name switch
        {
            TaskToolNames.CreateTask => await ExecuteCreateTaskAsync(userId, toolCall, cancellationToken),
            TaskToolNames.ListTasks => await ExecuteListTasksAsync(userId, toolCall, cancellationToken),
            TaskToolNames.GetTask => await ExecuteGetTaskAsync(userId, toolCall, cancellationToken),
            TaskToolNames.UpdateTask => await ExecuteUpdateTaskAsync(userId, toolCall, cancellationToken),
            TaskToolNames.DeleteTask => await ExecuteDeleteTaskAsync(userId, toolCall, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolCall.Name}")
        };
    }

    private async Task<TaskToolExecutionResult> ExecuteCreateTaskAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        CreateTaskArguments arguments;

        try
        {
            arguments = JsonSerializer.Deserialize<CreateTaskArguments>(toolCall.Arguments, JsonOptions)
                ?? throw new JsonException("Tool arguments were null.");
        }
        catch (JsonException)
        {
            return ErrorResult("Tool arguments could not be parsed.");
        }

        if (string.IsNullOrWhiteSpace(arguments.Title))
        {
            return ErrorResult("Title is required.");
        }

        var title = arguments.Title.Trim();

        if (title.Length > TaskTitle.MaxLength)
        {
            return ErrorResult($"Title must be at most {TaskTitle.MaxLength} characters long.");
        }

        var dueDate = NaturalDueDateParser.TryParse(arguments.DueDate, _timeProvider);

        if (dueDate is null)
        {
            return ErrorResult("A valid dueDate in ISO format (YYYY-MM-DD) is required.");
        }

        var description = string.IsNullOrWhiteSpace(arguments.Description)
            ? string.Empty
            : arguments.Description.Trim();

        try
        {
            var createRequest = new CreateTaskRequest(title, description, dueDate.Value, userId);
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

    private async Task<TaskToolExecutionResult> ExecuteListTasksAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        ListTasksArguments arguments;

        try
        {
            arguments = JsonSerializer.Deserialize<ListTasksArguments>(toolCall.Arguments, JsonOptions)
                ?? new ListTasksArguments(null, null, null);
        }
        catch (JsonException)
        {
            return ErrorResult("Tool arguments could not be parsed.");
        }

        var pageSize = arguments.PageSize ?? DefaultPageSize;

        if (pageSize <= 0 || pageSize > MaxPageSize)
        {
            return ErrorResult($"pageSize must be between 1 and {MaxPageSize}.");
        }

        var searchRequest = new TaskSearchRequest
        {
            PageNumber = 1,
            PageSize = pageSize,
            Title = string.IsNullOrWhiteSpace(arguments.Title) ? null : arguments.Title.Trim(),
            Status = string.IsNullOrWhiteSpace(arguments.Status) ? null : arguments.Status.Trim()
        };

        try
        {
            var result = await _taskService.SearchTasksAsync(userId, searchRequest, cancellationToken);
            var action = new TaskAssistantAction(TaskAssistantActionTypes.Listed);

            return new TaskToolExecutionResult(
                JsonSerializer.Serialize(BuildListTasksPayload(result)),
                action);
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

    private async Task<TaskToolExecutionResult> ExecuteGetTaskAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryParseTaskId(toolCall.Arguments, out var taskId, out var parseError))
        {
            return ErrorResult(parseError);
        }

        try
        {
            var task = await _taskService.GetTaskByIdAsync(taskId, userId, cancellationToken);

            var payload = new
            {
                success = true,
                task = new
                {
                    taskId = task.Id,
                    title = task.Title,
                    description = task.Description,
                    status = task.Status,
                    dueDate = task.DueDate,
                    createdDate = task.CreatedDate
                }
            };

            return new TaskToolExecutionResult(JsonSerializer.Serialize(payload));
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

    private async Task<TaskToolExecutionResult> ExecuteUpdateTaskAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        UpdateTaskArguments arguments;

        try
        {
            arguments = JsonSerializer.Deserialize<UpdateTaskArguments>(toolCall.Arguments, JsonOptions)
                ?? throw new JsonException("Tool arguments were null.");
        }
        catch (JsonException)
        {
            return ErrorResult("Tool arguments could not be parsed.");
        }

        if (!TryParseGuid(arguments.TaskId, out var taskId, out var taskIdError))
        {
            return ErrorResult(taskIdError);
        }

        if (arguments.Title is null && arguments.Description is null && arguments.Status is null)
        {
            return ErrorResult("At least one of title, description, or status must be provided.");
        }

        TaskResponse existing;

        try
        {
            existing = await _taskService.GetTaskByIdAsync(taskId, userId, cancellationToken);
        }
        catch (ValidationException ex)
        {
            return ErrorResult(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return ErrorResult(ex.Message);
        }

        var title = arguments.Title is null
            ? existing.Title
            : arguments.Title.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            return ErrorResult("Title is required.");
        }

        if (title.Length > TaskTitle.MaxLength)
        {
            return ErrorResult($"Title must be at most {TaskTitle.MaxLength} characters long.");
        }

        var description = arguments.Description is null
            ? existing.Description
            : arguments.Description.Trim();

        var status = arguments.Status is null
            ? existing.Status
            : arguments.Status.Trim();

        try
        {
            var updateRequest = new UpdateTaskRequest(taskId, title, description, status);
            await _taskService.UpdateTaskAsync(updateRequest, userId, cancellationToken);

            var updated = await _taskService.GetTaskByIdAsync(taskId, userId, cancellationToken);
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

    private async Task<TaskToolExecutionResult> ExecuteDeleteTaskAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryParseTaskId(toolCall.Arguments, out var taskId, out var parseError))
        {
            return ErrorResult(parseError);
        }

        TaskResponse existing;

        try
        {
            existing = await _taskService.GetTaskByIdAsync(taskId, userId, cancellationToken);
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
            await _taskService.DeleteTaskAsync(taskId, userId, cancellationToken);

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

    private static bool TryParseTaskId(string argumentsJson, out Guid taskId, out string error)
    {
        taskId = Guid.Empty;
        error = string.Empty;

        TaskIdArguments arguments;

        try
        {
            arguments = JsonSerializer.Deserialize<TaskIdArguments>(argumentsJson, JsonOptions)
                ?? throw new JsonException("Tool arguments were null.");
        }
        catch (JsonException)
        {
            error = "Tool arguments could not be parsed.";
            return false;
        }

        return TryParseGuid(arguments.TaskId, out taskId, out error);
    }

    private static bool TryParseGuid(string? value, out Guid taskId, out string error)
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
        var payload = new { error = message };
        return new TaskToolExecutionResult(JsonSerializer.Serialize(payload));
    }

    private sealed record CreateTaskArguments(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("dueDate")] string? DueDate);

    private sealed record ListTasksArguments(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("pageSize")] int? PageSize);

    private sealed record TaskIdArguments(
        [property: JsonPropertyName("taskId")] string? TaskId);

    private sealed record UpdateTaskArguments(
        [property: JsonPropertyName("taskId")] string? TaskId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("status")] string? Status);
}
