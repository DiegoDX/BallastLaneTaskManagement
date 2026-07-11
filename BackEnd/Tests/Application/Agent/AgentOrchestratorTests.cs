using Application.Agent;
using Application.DTOs.Agent;
using Application.Exceptions;
using FluentAssertions;
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

        var orchestrator = new AgentOrchestrator(
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

        var orchestrator = new AgentOrchestrator(
            [CreateCompletedHandler(AgentPhaseNames.Plan)],
            runStoreMock.Object);

        var act = () => orchestrator.ContinueAsync(
            Guid.NewGuid(),
            new AgentContinueRequest(Guid.NewGuid(), true));

        await act.Should().ThrowAsync<NotFoundException>();
    }

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
}
