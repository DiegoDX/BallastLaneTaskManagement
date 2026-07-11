using Application.Agent;
using Application.DTOs.Agent;
using FluentAssertions;
using Infrastructure.Agent;
using Microsoft.Extensions.Options;

namespace Tests.Infrastructure.Agent;

public sealed class InMemoryAgentRunStoreTests
{
    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenRunExpired()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryAgentRunStore(
            Options.Create(new AgentOptions { RunTtlMinutes = 30 }),
            timeProvider);

        var runId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new AgentRunContext
        {
            RunId = runId,
            UserId = userId,
            Messages = [new AgentMessageDto("user", "Organize tasks")],
            Status = AgentRunStatus.AwaitingApproval
        };

        await store.SaveAsync(context);

        timeProvider.Advance(TimeSpan.FromMinutes(31));

        var result = await store.GetAsync(runId, userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenUserDoesNotMatch()
    {
        var store = new InMemoryAgentRunStore(
            Options.Create(new AgentOptions()),
            TimeProvider.System);

        var runId = Guid.NewGuid();
        var context = new AgentRunContext
        {
            RunId = runId,
            UserId = Guid.NewGuid(),
            Messages = [new AgentMessageDto("user", "Organize tasks")]
        };

        await store.SaveAsync(context);

        var result = await store.GetAsync(runId, Guid.NewGuid());

        result.Should().BeNull();
    }

    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _utcNow = start;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
