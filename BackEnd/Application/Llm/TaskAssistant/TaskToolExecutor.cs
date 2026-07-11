using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Llm;

namespace Application.Llm.TaskAssistant;

public sealed class TaskToolExecutor : ITaskToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ITaskToolHandlers _handlers;

    public TaskToolExecutor(ITaskToolHandlers handlers)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    public Task<TaskToolExecutionResult> ExecuteAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        return toolCall.Name switch
        {
            TaskToolNames.CreateTask => ExecuteCreateTaskAsync(userId, toolCall, cancellationToken),
            TaskToolNames.ListTasks => ExecuteListTasksAsync(userId, toolCall, cancellationToken),
            TaskToolNames.GetTask => ExecuteGetTaskAsync(userId, toolCall, cancellationToken),
            TaskToolNames.UpdateTask => ExecuteUpdateTaskAsync(userId, toolCall, cancellationToken),
            TaskToolNames.DeleteTask => ExecuteDeleteTaskAsync(userId, toolCall, cancellationToken),
            _ => Task.FromResult(ErrorResult($"Unknown tool: {toolCall.Name}"))
        };
    }

    private Task<TaskToolExecutionResult> ExecuteCreateTaskAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize<CreateTaskArguments>(toolCall.Arguments, out var arguments, out var error))
        {
            return Task.FromResult(ErrorResult(error));
        }

        return _handlers.CreateTaskAsync(
            userId,
            arguments.Title,
            arguments.Description,
            arguments.DueDate,
            cancellationToken);
    }

    private Task<TaskToolExecutionResult> ExecuteListTasksAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize<ListTasksArguments>(toolCall.Arguments, out var arguments, out var error))
        {
            return Task.FromResult(ErrorResult(error));
        }

        return _handlers.SearchTasksAsync(
            userId,
            null,
            arguments.Status,
            arguments.Title,
            arguments.PageSize,
            cancellationToken);
    }

    private Task<TaskToolExecutionResult> ExecuteGetTaskAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize<TaskIdArguments>(toolCall.Arguments, out var arguments, out var error))
        {
            return Task.FromResult(ErrorResult(error));
        }

        return _handlers.SearchTasksAsync(
            userId,
            arguments.TaskId,
            null,
            null,
            1,
            cancellationToken);
    }

    private Task<TaskToolExecutionResult> ExecuteUpdateTaskAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize<UpdateTaskArguments>(toolCall.Arguments, out var arguments, out var error))
        {
            return Task.FromResult(ErrorResult(error));
        }

        return _handlers.UpdateTaskAsync(
            userId,
            arguments.TaskId,
            arguments.Title,
            arguments.Description,
            arguments.Status,
            cancellationToken);
    }

    private Task<TaskToolExecutionResult> ExecuteDeleteTaskAsync(
        Guid userId,
        LlmToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize<TaskIdArguments>(toolCall.Arguments, out var arguments, out var error))
        {
            return Task.FromResult(ErrorResult(error));
        }

        return _handlers.DeleteTaskAsync(userId, arguments.TaskId, cancellationToken);
    }

    private static bool TryDeserialize<T>(
        string argumentsJson,
        out T arguments,
        out string error)
        where T : class
    {
        arguments = null!;
        error = string.Empty;

        try
        {
            arguments = JsonSerializer.Deserialize<T>(argumentsJson, JsonOptions)
                ?? throw new JsonException("Tool arguments were null.");
            return true;
        }
        catch (JsonException)
        {
            error = "Tool arguments could not be parsed.";
            return false;
        }
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
