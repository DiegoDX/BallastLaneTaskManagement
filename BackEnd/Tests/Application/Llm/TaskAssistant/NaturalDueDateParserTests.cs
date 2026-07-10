using Application.Llm.TaskAssistant;
using FluentAssertions;

namespace Tests.Application.Llm.TaskAssistant;

public sealed class NaturalDueDateParserTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 7, 9, 15, 30, 0, DateTimeKind.Utc);
    private static readonly TimeProvider TimeProvider = new FakeTimeProvider(FixedUtcNow);

    [Theory]
    [InlineData("2026-07-10")]
    [InlineData("2026-07-10T00:00:00")]
    [InlineData("2026-07-10T12:30:00Z")]
    public void TryParse_returns_utc_date_for_valid_iso_input(string dueDate)
    {
        var result = NaturalDueDateParser.TryParse(dueDate, TimeProvider);

        result.Should().Be(new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_returns_null_when_input_is_missing(string? dueDate)
    {
        var result = NaturalDueDateParser.TryParse(dueDate, TimeProvider);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-13-40")]
    [InlineData("tomorrow")]
    public void TryParse_returns_null_for_invalid_input(string dueDate)
    {
        var result = NaturalDueDateParser.TryParse(dueDate, TimeProvider);

        result.Should().BeNull();
    }

    [Fact]
    public void TryParse_trims_whitespace_before_parsing()
    {
        var result = NaturalDueDateParser.TryParse("  2026-07-10  ", TimeProvider);

        result.Should().Be(new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void TryParse_throws_when_time_provider_is_null()
    {
        var act = () => NaturalDueDateParser.TryParse("2026-07-10", null!);

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
