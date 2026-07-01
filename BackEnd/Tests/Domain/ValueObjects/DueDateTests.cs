using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Domain.ValueObjects;

public sealed class DueDateTests
{
    private static readonly Guid ValidTaskId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ValidUserId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTime ValidCreatedAtUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_rejects_past_dates()
    {
        // Arrange
        var act = () => DueDate.Create(DateTime.Today.AddDays(-1));

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Due date must be in the future.");
    }

    [Fact]
    public void Create_accepts_today_as_due_date()
    {
        // Arrange & Act
        var dueDate = DueDate.Create(DateTime.Today);

        // Assert
        dueDate.Value.Should().Be(DateTime.Today);
    }

    [Fact]
    public void Create_rejects_default_date()
    {
        // Arrange
        var act = () => DueDate.Create(default);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Due date cannot be empty.");
    }

    [Fact]
    public void FromPersistence_rejects_default_date()
    {
        // Arrange
        var act = () => TaskItem.Restore(
            ValidTaskId,
            ValidUserId,
            "Valid title",
            null,
            TaskItemStatus.Pending,
            default,
            ValidCreatedAtUtc);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Due date cannot be empty.");
    }

    [Fact]
    public void FromPersistence_allows_past_dates()
    {
        // Arrange
        var pastDate = DateTime.Today.AddDays(-30);

        // Act
        var task = TaskItem.Restore(
            ValidTaskId,
            ValidUserId,
            "Valid title",
            null,
            TaskItemStatus.Pending,
            pastDate,
            ValidCreatedAtUtc);

        // Assert
        task.DueDate.Value.Should().Be(pastDate);
    }
}
