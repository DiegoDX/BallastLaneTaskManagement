using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;

namespace Application.Services;

public sealed class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;

    public TaskService(ITaskRepository taskRepository, IUserRepository userRepository)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<TaskResponse> CreateTaskAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateTaskRequest(request);

        var user = await _userRepository
            .GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"User with id '{request.UserId}' was not found.");
        }

        TaskItem task;
        try
        {
            task = TaskItem.Create(
                Guid.NewGuid(),
                request.UserId,
                request.Title,
                request.DueDate,
                request.Description);
        }
        catch (DomainValidationException ex)
        {
            throw new ValidationException(ex.Message);
        }

        await _taskRepository.AddAsync(task, cancellationToken);

        return TaskMapper.ToResponse(task);
    }

    public async Task UpdateTaskAsync(
        UpdateTaskRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUpdateTaskRequest(request);

        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }

        var task = await _taskRepository
            .GetByIdAsync(request.TaskId, cancellationToken);

        if (task is null)
        {
            throw new NotFoundException($"Task with id '{request.TaskId}' was not found.");
        }

        if (task.UserId != userId)
        {
            throw new NotFoundException($"Task with id '{request.TaskId}' was not found.");
        }

        var requestedStatus = ParseStatus(request.Status);
        EnsureValidStatusTransition(task.Status, requestedStatus);

        try
        {
            task.UpdateTitle(request.Title);
            task.UpdateStatus(requestedStatus);
            task.UpdateDescription(request.Description);
        }
        catch (DomainValidationException ex)
        {
            throw new ValidationException(ex.Message);
        }

        await _taskRepository.UpdateAsync(task, cancellationToken);
    }

    public async Task<TaskResponse> GetTaskByIdAsync(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty)
        {
            throw new ValidationException("Task id is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }

        var task = await _taskRepository
            .GetByIdAsync(taskId, cancellationToken);

        if (task is null)
        {
            throw new NotFoundException($"Task with id '{taskId}' was not found.");
        }

        if (task.UserId != userId)
        {
            throw new NotFoundException($"Task with id '{taskId}' was not found.");
        }

        return TaskMapper.ToResponse(task);
    }

    public async Task<IReadOnlyList<TaskResponse>> GetTasksByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }

        var user = await _userRepository
            .GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"User with id '{userId}' was not found.");
        }

        var tasks = await _taskRepository
            .GetByUserIdAsync(userId, cancellationToken);

        return tasks.Select(TaskMapper.ToResponse).ToList();
    }

    public async Task DeleteTaskAsync(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty)
        {
            throw new ValidationException("Task id is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }

        var task = await _taskRepository
            .GetByIdAsync(taskId, cancellationToken);

        if (task is null)
        {
            throw new NotFoundException($"Task with id '{taskId}' was not found.");
        }

        if (task.UserId != userId)
        {
            throw new NotFoundException($"Task with id '{taskId}' was not found.");
        }

        await _taskRepository.DeleteAsync(taskId, cancellationToken);
    }

    private static void ValidateCreateTaskRequest(CreateTaskRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Create task request is required.");
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException("Title is required.");
        }

        if (request.DueDate == default)
        {
            throw new ValidationException("Due date is required.");
        }
    }

    private static void ValidateUpdateTaskRequest(UpdateTaskRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Update task request is required.");
        }

        if (request.TaskId == Guid.Empty)
        {
            throw new ValidationException("Task id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new ValidationException("Status is required.");
        }
    }

    private static TaskItemStatus ParseStatus(string status)
    {
        if (!Enum.TryParse<TaskItemStatus>(status, ignoreCase: true, out var parsedStatus)
            || !Enum.IsDefined(parsedStatus))
        {
            throw new ValidationException("Status must be one of: Pending, InProgress, or Completed.");
        }

        return parsedStatus;
    }

    private static void EnsureValidStatusTransition(TaskItemStatus current, TaskItemStatus requested)
    {
        if (current == requested)
        {
            return;
        }

        if (current == TaskItemStatus.Completed)
        {
            throw new ValidationException("A completed task cannot change status.");
        }

        var isValid = (current, requested) switch
        {
            (TaskItemStatus.Pending, TaskItemStatus.InProgress) => true,
            (TaskItemStatus.Pending, TaskItemStatus.Completed) => true,
            (TaskItemStatus.InProgress, TaskItemStatus.Pending) => true,
            (TaskItemStatus.InProgress, TaskItemStatus.Completed) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new ValidationException(
                $"Cannot transition task status from '{current}' to '{requested}'.");
        }
    }
}
