using Application.Agent;
using Application.DTOs.Agent;
using Application.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests.Application.Agent;

public sealed class AgentOrchestratorTests
{
    [Fact]
    public async Task RunAsync_ShouldStopEarly_WhenPhaseRequestsStop()
    {
        var runStoreMock = new Mock<IAgentRunStore>();
        var stopHandler = new StubPhaseHandler(AgentPhaseNames.Approval, new AgentPhaseOutcome(
            AgentPhaseStatus.Waiting,
            ShouldStop: true));

        var orchestrator = CreateOrchestrator(
            [CreateCompletedHandler(AgentPhaseNames.Plan), stopHandler],
            runStoreMock.Object);

        runStoreMock
            .Setup(store => store.SaveAsync(It.IsAny<AgentRunContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await orchestrator.RunAsync(
            Guid.NewGuid(),
            new AgentRequest([new AgentMessageDto("user", "Delete all tasks")]));

        response.Status.Should().Be(AgentRunStatus.AwaitingApproval);
        response.Phases.Should().HaveCount(2);
        response.Phases[1].Phase.Should().Be(AgentPhaseNames.Approval);
    }

    [Fact]
    public async Task ContinueAsync_ShouldThrowNotFoundException_WhenRunDoesNotExist()
    {
        var runStoreMock = new Mock<IAgentRunStore>();
        runStoreMock
            .Setup(store => store.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRunContext?)null);

        var orchestrator = CreateOrchestrator(
            [CreateCompletedHandler(AgentPhaseNames.Plan)],
            runStoreMock.Object);

        var act = () => orchestrator.ContinueAsync(
            Guid.NewGuid(),
            new AgentContinueRequest(Guid.NewGuid(), true));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RunAsync_ShouldReExecuteAfterReview_WhenReviewerRequestsIt()
    {
        var executeCount = 0;
        var reviewCount = 0;

        var executeHandler = new CallbackPhaseHandler(AgentPhaseNames.Execute, context =>
        {
            executeCount++;
            context.ExecutionReport = new AgentExecutionReport(1, []);
            return new AgentPhaseOutcome(AgentPhaseStatus.Completed, "{}");
        });

        var reviewHandler = new CallbackPhaseHandler(AgentPhaseNames.Review, context =>
        {
            reviewCount++;

            if (reviewCount == 1)
            {
                context.Review = new AgentReview(
                    false,
                    ["Incomplete execution"],
                    ["Retry"],
                    RequiresReExecution: true,
                    ReExecutionHint: "Retry failed updates.");

                return new AgentPhaseOutcome(AgentPhaseStatus.Completed, "{}");
            }

            context.Review = new AgentReview(true, [], []);
            return new AgentPhaseOutcome(AgentPhaseStatus.Completed, "{}");
        });

        var orchestrator = CreateOrchestrator(
        [
            CreateCompletedHandler(AgentPhaseNames.Plan),
            CreateCompletedHandler(AgentPhaseNames.Approval),
            executeHandler,
            reviewHandler,
            CreateCompletedHandler(AgentPhaseNames.Summary)
        ],
        Mock.Of<IAgentRunStore>(),
        maxReExecutionAttempts: 1);

        var response = await orchestrator.RunAsync(
            Guid.NewGuid(),
            new AgentRequest([new AgentMessageDto("user", "Fix my tasks")]));

        executeCount.Should().Be(2);
        reviewCount.Should().Be(2);
        response.Phases.Count(phase => phase.Phase == AgentPhaseNames.Execute).Should().Be(2);
        response.Phases.Count(phase => phase.Phase == AgentPhaseNames.Review).Should().Be(2);
        response.Status.Should().Be(AgentRunStatus.Completed);
    }

    private static AgentOrchestrator CreateOrchestrator(
        IReadOnlyList<IAgentPhaseHandler> handlers,
        IAgentRunStore runStore,
        int maxReExecutionAttempts = 2) =>
        new(
            handlers,
            runStore,
            Options.Create(new AgentOptions { MaxReExecutionAttempts = maxReExecutionAttempts }));

    private static StubPhaseHandler CreateCompletedHandler(string phaseName) =>
        new(phaseName, new AgentPhaseOutcome(AgentPhaseStatus.Completed, "{}"));

    private sealed class StubPhaseHandler(string phaseName, AgentPhaseOutcome outcome) : IAgentPhaseHandler
    {
        public string PhaseName { get; } = phaseName;

        public Task<AgentPhaseOutcome> HandleAsync(
            AgentRunContext context,
            CancellationToken cancellationToken = default)
        {
            if (phaseName == AgentPhaseNames.Approval && outcome.ShouldStop)
            {
                context.Status = AgentRunStatus.AwaitingApproval;
            }

            return Task.FromResult(outcome);
        }
    }

    private sealed class CallbackPhaseHandler(
        string phaseName,
        Func<AgentRunContext, AgentPhaseOutcome> callback) : IAgentPhaseHandler
    {
        public string PhaseName { get; } = phaseName;

        public Task<AgentPhaseOutcome> HandleAsync(
            AgentRunContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(callback(context));
    }
}
