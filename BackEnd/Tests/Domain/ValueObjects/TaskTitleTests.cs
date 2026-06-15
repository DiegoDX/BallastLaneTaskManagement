using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Domain.ValueObjects;

public sealed class TaskTitleTests
{
    private static readonly Guid ValidTaskId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ValidUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTime ValidDueDate = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_rejects_title_exceeding_max_length()
    {
        // Arrange
        var tooLongTitle = new string('a', TaskTitle.MaxLength + 1);

        // Act
        var act = () => TaskTitle.Create(tooLongTitle);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Title must be at most {MaxLength} characters long.");
    }

    [Fact]
    public void FromPersistence_rejects_empty_title()
    {
        // Arrange
        var act = () => TaskItem.Restore(
            ValidTaskId,
            ValidUserId,
            "   ",
            null,
            TaskItemStatus.Pending,
            ValidDueDate);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Title cannot be empty or whitespace.");
    }

    [Fact]
    public void FromPersistence_rejects_title_exceeding_max_length()
    {
        // Arrange
        var tooLongTitle = new string('a', TaskTitle.MaxLength + 1);

        // Act
        var act = () => TaskItem.Restore(
            ValidTaskId,
            ValidUserId,
            tooLongTitle,
            null,
            TaskItemStatus.Pending,
            ValidDueDate);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Title must be at most {MaxLength} characters long.");
    }
}
