using Application.Agent;
using Application.Agent.Phases;
using Application.DTOs.Agent;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Mcp;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests.Application.Agent.Phases;

public sealed class ExecutePhaseHandlerMcpTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly Mock<IMcpToolClient> _mcpToolClientMock = new();
    private readonly ExecutePhaseHandler _sut;

    public ExecutePhaseHandlerMcpTests()
    {
        _sut = new ExecutePhaseHandler(
            _llmClientMock.Object,
            _mcpToolClientMock.Object,
            Options.Create(new AgentOptions
            {
                MaxExecuteIterations = 3,
                MaxToolCallsPerIteration = 2
            }));
    }

    [Fact]
    public async Task HandleAsync_ShouldListToolsAndExecuteUntilNoToolCalls()
    {
        var userId = Guid.NewGuid();
        var context = CreateContext(userId);

        _mcpToolClientMock
            .Setup(client => client.ListToolsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new LlmToolDefinition("search_tasks", "Search tasks", "{}")
            ]);

        _llmClientMock
            .SetupSequence(client => client.CompleteChatWithToolsAsync(
                It.IsAny<LlmChatRequest>(),
                It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatCompletion(
                string.Empty,
                [new LlmToolCall("call-1", "search_tasks", """{"status":"Pending"}""")],
                "gpt-test"))
            .ReturnsAsync(new LlmChatCompletion("All done.", [], "gpt-test"));

        _mcpToolClientMock
            .Setup(client => client.CallToolAsync(
                userId,
                "search_tasks",
                """{"status":"Pending"}""",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult(
                """{"success":true,"tasks":[]}""",
                new TaskAssistantAction(TaskAssistantActionTypes.Listed)));

        var outcome = await _sut.HandleAsync(context);

        outcome.Status.Should().Be(AgentPhaseStatus.Completed);
        context.ExecutionReport.Should().NotBeNull();
        context.ExecutionReport!.Iterations.Should().Be(2);
        context.ExecutionReport.ToolCalls.Should().ContainSingle(record =>
            record.Name == "search_tasks" && record.Success);
        context.Actions.Should().ContainSingle(action => action.Type == TaskAssistantActionTypes.Listed);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenMaxIterationsExceeded()
    {
        var userId = Guid.NewGuid();
        var context = CreateContext(userId);

        _mcpToolClientMock
            .Setup(client => client.ListToolsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LlmToolDefinition("search_tasks", "Search tasks", "{}")]);

        _llmClientMock
            .Setup(client => client.CompleteChatWithToolsAsync(
                It.IsAny<LlmChatRequest>(),
                It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatCompletion(
                string.Empty,
                [new LlmToolCall("call-1", "search_tasks", "{}")],
                "gpt-test"));

        _mcpToolClientMock
            .Setup(client => client.CallToolAsync(
                userId,
                "search_tasks",
                "{}",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult("""{"success":true,"tasks":[]}"""));

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
