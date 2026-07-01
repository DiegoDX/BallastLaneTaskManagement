using Domain.Enums;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities;

public sealed class TaskItem
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public TaskTitle Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public DueDate DueDate { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    private TaskItem()
    {
    }

    public static TaskItem Create(
        Guid id,
        Guid userId,
        string title,
        DateTime dueDate,
        DateTime createdAtUtc,
        string? description = null,
        TaskItemStatus status = TaskItemStatus.Pending)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Task id cannot be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("User id cannot be empty.");
        }

        if (createdAtUtc == default)
        {
            throw new DomainValidationException("Created date cannot be empty.");
        }

        ValidateStatus(status);

        return new TaskItem
        {
            Id = id,
            UserId = userId,
            Title = TaskTitle.Create(title),
            Description = NormalizeDescription(description),
            Status = status,
            DueDate = DueDate.Create(dueDate),
            CreatedAtUtc = createdAtUtc
        };
    }

    public static TaskItem Restore(
        Guid id,
        Guid userId,
        string title,
        string? description,
        TaskItemStatus status,
        DateTime dueDate,
        DateTime createdAtUtc)
    {
        if (createdAtUtc == default)
        {
            throw new DomainValidationException("Created date cannot be empty.");
        }

        ValidateStatus(status);

        return new TaskItem
        {
            Id = id,
            UserId = userId,
            Title = TaskTitle.FromPersistence(title),
            Description = description,
            Status = status,
            DueDate = DueDate.FromPersistence(dueDate),
            CreatedAtUtc = createdAtUtc
        };
    }

    public void UpdateTitle(string title)
    {
        Title = TaskTitle.Create(title);
    }

    public void UpdateDescription(string? description)
    {
        Description = NormalizeDescription(description);
    }

    public void UpdateStatus(TaskItemStatus status)
    {
        ValidateStatus(status);
        Status = status;
    }

    public void UpdateDueDate(DateTime dueDate)
    {
        DueDate = DueDate.Create(dueDate);
    }

    public void MarkInProgress()
    {
        UpdateStatus(TaskItemStatus.InProgress);
    }

    public void MarkCompleted()
    {
        UpdateStatus(TaskItemStatus.Completed);
    }

    public void MarkPending()
    {
        UpdateStatus(TaskItemStatus.Pending);
    }

    private static void ValidateStatus(TaskItemStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new DomainValidationException(
                "Status must be one of: Pending, InProgress, or Completed.");
        }
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return description.Trim();
    }
}
