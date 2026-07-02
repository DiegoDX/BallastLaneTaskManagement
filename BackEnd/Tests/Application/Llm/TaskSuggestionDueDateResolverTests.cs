using Application.Llm;
using FluentAssertions;

namespace Tests.Application.Llm;

public sealed class TaskSuggestionDueDateResolverTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 7, 1, 15, 30, 0, DateTimeKind.Utc);
    private static readonly TimeProvider TimeProvider = new FakeTimeProvider(FixedUtcNow);

    [Fact]
    public void Resolve_uses_override_due_date_when_provided()
    {
        // Arrange
        var overrideDueDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var rootDueDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = TaskSuggestionDueDateResolver.Resolve(
            overrideDueDate,
            rootDueDate,
            TimeProvider);

        // Assert
        result.Should().Be(overrideDueDate);
    }

    [Fact]
    public void Resolve_uses_root_due_date_when_override_is_missing()
    {
        // Arrange
        var rootDueDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = TaskSuggestionDueDateResolver.Resolve(
            overrideDueDate: null,
            rootDueDate,
            TimeProvider);

        // Assert
        result.Should().Be(rootDueDate);
    }

    [Fact]
    public void Resolve_generates_random_due_date_within_zero_to_thirty_days_when_no_dates_provided()
    {
        // Arrange
        var random = new Random(42);
        var today = FixedUtcNow.Date;

        // Act
        var result = TaskSuggestionDueDateResolver.Resolve(
            overrideDueDate: null,
            rootDueDate: null,
            TimeProvider,
            random);

        // Assert
        var dayOffset = (result - today).Days;
        dayOffset.Should().BeInRange(0, 30);
    }

    [Fact]
    public void Resolve_ignores_default_override_and_uses_root_due_date()
    {
        // Arrange
        var rootDueDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = TaskSuggestionDueDateResolver.Resolve(
            overrideDueDate: default,
            rootDueDate,
            TimeProvider);

        // Assert
        result.Should().Be(rootDueDate);
    }

    [Fact]
    public void Resolve_throws_when_time_provider_is_null()
    {
        // Act
        var act = () => TaskSuggestionDueDateResolver.Resolve(null, null, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTime _utcNow;

        public FakeTimeProvider(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => new(_utcNow);
    }
}
