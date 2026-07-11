using Application.Agent;
using Application.Agent.Phases;
using Application.DTOs.Agent;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Tests.Application.Agent.Phases;

public sealed class ApprovalPhaseHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldSkip_WhenPlanDoesNotRequireApproval()
    {
        var handler = CreateHandler();
        var context = CreateContext(requiresApproval: false);

        var outcome = await handler.HandleAsync(context);

        outcome.Status.Should().Be(AgentPhaseStatus.Skipped);
        outcome.ShouldStop.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_ShouldStopAndWait_WhenApprovalIsRequiredOnFirstRun()
    {
        var handler = CreateHandler();
        var context = CreateContext(requiresApproval: true);

        var outcome = await handler.HandleAsync(context);

        outcome.Status.Should().Be(AgentPhaseStatus.Waiting);
        outcome.ShouldStop.Should().BeTrue();
        context.Status.Should().Be(AgentRunStatus.AwaitingApproval);
    }

    [Fact]
    public async Task HandleAsync_ShouldReject_WhenContinuationIsNotApproved()
    {
        var handler = CreateHandler();
        var context = CreateContext(requiresApproval: true);
        context.IsContinuation = true;
        context.Approved = false;

        var outcome = await handler.HandleAsync(context);

        outcome.Status.Should().Be(AgentPhaseStatus.Completed);
        outcome.ShouldStop.Should().BeTrue();
        context.Status.Should().Be(AgentRunStatus.Rejected);
        context.Summary.Should().Be("The plan was rejected. No changes were made.");
    }

    private static ApprovalPhaseHandler CreateHandler() =>
        new(Options.Create(new AgentOptions()));

    private static AgentRunContext CreateContext(bool requiresApproval)
    {
        var context = new AgentRunContext
        {
            RunId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Messages = [new AgentMessageDto("user", "Organize my tasks")]
        };

        context.Plan = new AgentPlan(
            "Organize tasks",
            [new AgentPlanStep(1, "List tasks", "list_tasks")],
            requiresApproval,
            "medium");

        return context;
    }
}
