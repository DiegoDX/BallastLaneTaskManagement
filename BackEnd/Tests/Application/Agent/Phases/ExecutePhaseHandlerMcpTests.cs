using Application.Agent;
using Application.Agent.Phases;
using Application.Agent.Specialists;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.Exceptions;
using FluentAssertions;
using Moq;

namespace Tests.Application.Agent.Phases;

public sealed class ExecutePhaseHandlerMcpTests
{
    private readonly Mock<IExecutorAgent> _executorAgentMock = new();
    private readonly ExecutePhaseHandler _sut;

    public ExecutePhaseHandlerMcpTests()
    {
        _sut = new ExecutePhaseHandler(_executorAgentMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapExecutorResultToContext()
    {
        var userId = Guid.NewGuid();
        var context = CreateContext(userId);
        var executeMessages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, "system")
        };

        _executorAgentMock
            .Setup(agent => agent.ExecuteAsync(It.IsAny<ExecutorAgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutorAgentResult(
                new AgentExecutionReport(2, [new AgentToolCallRecord("search_tasks", true)]),
                [new TaskAssistantAction(TaskAssistantActionTypes.Listed)],
                executeMessages,
                "All done.",
                "gpt-test"));

        var outcome = await _sut.HandleAsync(context);

        outcome.Status.Should().Be(AgentPhaseStatus.Completed);
        context.ExecutionReport.Should().NotBeNull();
        context.ExecutionReport!.Iterations.Should().Be(2);
        context.Actions.Should().ContainSingle(action => action.Type == TaskAssistantActionTypes.Listed);
        context.ExecuteMessages.Should().BeEquivalentTo(executeMessages);
        context.Model.Should().Be("gpt-test");
    }

    [Fact]
    public async Task HandleAsync_ShouldPropagateExecutorExceptions()
    {
        var context = CreateContext(Guid.NewGuid());

        _executorAgentMock
            .Setup(agent => agent.ExecuteAsync(It.IsAny<ExecutorAgentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmException("Agent exceeded maximum execute iterations.", isTransient: false));

        var act = () => _sut.HandleAsync(context);

        await act.Should().ThrowAsync<LlmException>()
            .WithMessage("*maximum execute iterations*");
    }

    private static AgentRunContext CreateContext(Guid userId)
    {
        var context = new AgentRunContext
        {
            RunId = Guid.NewGuid(),
            UserId = userId,
            Messages = [new AgentMessageDto("user", "Search my pending tasks")]
        };

        context.Plan = new AgentPlan(
            "Search pending tasks",
            [new AgentPlanStep(1, "Search pending tasks", "search_tasks")],
            false,
            "low");

        return context;
    }
}
