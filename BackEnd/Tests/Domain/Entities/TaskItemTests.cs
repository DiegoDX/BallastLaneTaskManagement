using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;

namespace Tests.Domain.Entities;

public sealed class TaskItemTests
{
    private static readonly Guid ValidTaskId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ValidUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime ValidDueDate = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    private static TaskItem CreateValidTask(
        string title = "Prepare sprint review",
        string? description = "Collect metrics and demo notes",
        TaskItemStatus status = TaskItemStatus.Pending)
    {
        return TaskItem.Create(
            ValidTaskId,
            ValidUserId,
            title,
            ValidDueDate,
            description,
            status);
    }

    [Fact]
    public void Creating_a_task_assigns_a_valid_identifier_and_owner()
    {
        // Arrange & Act
        var task = CreateValidTask();

        // Assert
        task.Id.Should().Be(ValidTaskId);
        task.UserId.Should().Be(ValidUserId);
    }

    [Fact]
    public void A_new_task_starts_with_pending_status_by_default()
    {
        // Arrange & Act
        var task = CreateValidTask();

        // Assert
        task.Status.Should().Be(TaskItemStatus.Pending);
    }

    [Fact]
    public void Creating_a_task_without_an_identifier_is_rejected()
    {
        // Arrange
        var act = () => TaskItem.Create(
            Guid.Empty,
            ValidUserId,
            "Valid title",
            ValidDueDate);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Task id cannot be empty.");
    }

    [Fact]
    public void Creating_a_task_without_an_owner_is_rejected()
    {
        // Arrange
        var act = () => TaskItem.Create(
            ValidTaskId,
            Guid.Empty,
            "Valid title",
            ValidDueDate);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("User id cannot be empty.");
    }

    [Fact]
    public void Creating_a_task_without_a_title_is_rejected()
    {
        // Arrange
        var act = () => TaskItem.Create(
            ValidTaskId,
            ValidUserId,
            string.Empty,
            ValidDueDate);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Title cannot be empty or whitespace.");
    }

    [Fact]
    public void Creating_a_task_with_a_whitespace_title_is_rejected()
    {
        // Arrange
        var act = () => TaskItem.Create(
            ValidTaskId,
            ValidUserId,
            "   ",
            ValidDueDate);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Title cannot be empty or whitespace.");
    }

    [Fact]
    public void Creating_a_task_without_a_due_date_is_rejected()
    {
        // Arrange
        var act = () => TaskItem.Create(
            ValidTaskId,
            ValidUserId,
            "Valid title",
            default);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Due date cannot be empty.");
    }

    [Fact]
    public void Creating_a_task_with_an_undefined_status_is_rejected()
    {
        // Arrange
        var invalidStatus = (TaskItemStatus)999;

        var act = () => TaskItem.Create(
            ValidTaskId,
            ValidUserId,
            "Valid title",
            ValidDueDate,
            status: invalidStatus);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Status must be one of: Pending, InProgress, or Completed.");
    }

    [Fact]
    public void Creating_a_task_trims_the_title_and_normalizes_blank_description_to_null()
    {
        // Arrange & Act
        var task = TaskItem.Create(
            ValidTaskId,
            ValidUserId,
            "  Trim me  ",
            ValidDueDate,
            "  ");

        // Assert
        task.Title.Value.Should().Be("Trim me");
        task.Description.Should().BeNull();
    }

    [Fact]
    public void A_pending_task_can_be_marked_as_in_progress()
    {
        // Arrange
        var task = CreateValidTask();

        // Act
        task.MarkInProgress();

        // Assert
        task.Status.Should().Be(TaskItemStatus.InProgress);
        task.UserId.Should().Be(ValidUserId);
        task.Title.Value.Should().Be("Prepare sprint review");
    }

    [Fact]
    public void An_in_progress_task_can_be_marked_as_completed()
    {
        // Arrange
        var task = CreateValidTask(status: TaskItemStatus.InProgress);

        // Act
        task.MarkCompleted();

        // Assert
        task.Status.Should().Be(TaskItemStatus.Completed);
    }

    [Fact]
    public void A_pending_task_can_be_marked_as_completed_directly()
    {
        // Arrange
        var task = CreateValidTask();

        // Act
        task.MarkCompleted();

        // Assert
        task.Status.Should().Be(TaskItemStatus.Completed);
    }

    [Fact]
    public void A_completed_task_can_be_changed_to_another_valid_status_in_the_domain_model()
    {
        // Arrange
        var task = CreateValidTask(status: TaskItemStatus.Completed);

        // Act
        task.MarkPending();

        // Assert
        task.Status.Should().Be(TaskItemStatus.Pending);
    }

    [Fact]
    public void Updating_a_task_title_rejects_empty_values()
    {
        // Arrange
        var task = CreateValidTask();

        // Act
        var act = () => task.UpdateTitle("   ");

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Title cannot be empty or whitespace.");
    }

    [Fact]
    public void Updating_a_task_status_to_an_undefined_value_is_rejected()
    {
        // Arrange
        var task = CreateValidTask();
        var invalidStatus = (TaskItemStatus)999;

        // Act
        var act = () => task.UpdateStatus(invalidStatus);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Status must be one of: Pending, InProgress, or Completed.");
    }

    [Fact]
    public void Updating_a_task_due_date_rejects_an_empty_value()
    {
        // Arrange
        var task = CreateValidTask();

        // Act
        var act = () => task.UpdateDueDate(default);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Due date cannot be empty.");
    }

    [Fact]
    public void Updating_a_task_description_trims_text_and_clears_blank_values()
    {
        // Arrange
        var task = CreateValidTask(description: "Initial description");

        // Act
        task.UpdateDescription("  Updated description  ");
        task.UpdateDescription("   ");

        // Assert
        task.Description.Should().BeNull();
    }

    [Fact]
    public void Restoring_a_task_from_persistence_preserves_business_state()
    {
        // Arrange & Act
        var task = TaskItem.Restore(
            ValidTaskId,
            ValidUserId,
            "Persisted task",
            "Stored description",
            TaskItemStatus.InProgress,
            ValidDueDate);

        // Assert
        task.Id.Should().Be(ValidTaskId);
        task.UserId.Should().Be(ValidUserId);
        task.Title.Value.Should().Be("Persisted task");
        task.Description.Should().Be("Stored description");
        task.Status.Should().Be(TaskItemStatus.InProgress);
        task.DueDate.Value.Should().Be(ValidDueDate);
    }

    [Fact]
    public void Restoring_a_task_with_an_undefined_status_is_rejected()
    {
        // Arrange
        var act = () => TaskItem.Restore(
            ValidTaskId,
            ValidUserId,
            "Persisted task",
            null,
            (TaskItemStatus)999,
            ValidDueDate);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Status must be one of: Pending, InProgress, or Completed.");
    }

    [Fact]
    public void A_task_maintains_its_owner_after_multiple_status_changes()
    {
        // Arrange
        var task = CreateValidTask();

        // Act
        task.MarkInProgress();
        task.MarkCompleted();
        task.MarkPending();

        // Assert
        task.UserId.Should().Be(ValidUserId);
        task.Status.Should().Be(TaskItemStatus.Pending);
    }

    [Fact]
    public void Creating_a_task_with_a_past_due_date_is_rejected()
    {
        // Arrange
        var act = () => TaskItem.Create(
            ValidTaskId,
            ValidUserId,
            "Valid title",
            DateTime.Today.AddDays(-1));

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Due date must be in the future.");
    }

    [Fact]
    public void Creating_a_task_with_a_title_exceeding_max_length_is_rejected()
    {
        // Arrange
        var tooLongTitle = new string('a', 257);

        var act = () => TaskItem.Create(
            ValidTaskId,
            ValidUserId,
            tooLongTitle,
            ValidDueDate);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Title must be at most {MaxLength} characters long.");
    }

    [Fact]
    public void Updating_a_task_title_trims_and_applies_valid_value()
    {
        // Arrange
        var task = CreateValidTask();

        // Act
        task.UpdateTitle("  Updated title  ");

        // Assert
        task.Title.Value.Should().Be("Updated title");
    }

    [Fact]
    public void Updating_a_task_due_date_accepts_a_future_date()
    {
        // Arrange
        var task = CreateValidTask();
        var newDueDate = DateTime.Today.AddDays(10);

        // Act
        task.UpdateDueDate(newDueDate);

        // Assert
        task.DueDate.Value.Should().Be(newDueDate);
    }

    [Fact]
    public void Creating_a_task_trims_non_blank_description()
    {
        // Arrange & Act
        var task = TaskItem.Create(
            ValidTaskId,
            ValidUserId,
            "Valid title",
            ValidDueDate,
            "  Trimmed description  ");

        // Assert
        task.Description.Should().Be("Trimmed description");
    }
}
