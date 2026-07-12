using Application.Agent;
using Application.Agent.Specialists;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Mcp;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests.Application.Agent.Specialists;

public sealed class ExecutorAgentTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly Mock<IMcpToolClient> _mcpToolClientMock = new();
    private readonly ExecutorAgent _sut;

    public ExecutorAgentTests()
    {
        _sut = new ExecutorAgent(
            _llmClientMock.Object,
            _mcpToolClientMock.Object,
            Options.Create(new AgentOptions
            {
                MaxExecuteIterations = 3,
                MaxToolCallsPerIteration = 2
            }));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldListToolsAndExecuteUntilNoToolCalls()
    {
        var userId = Guid.NewGuid();
        var plan = CreatePlan();

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

        var result = await _sut.ExecuteAsync(
            new ExecutorAgentRequest(
                userId,
                [new AgentMessageDto("user", "Search my pending tasks")],
                plan,
                []));

        result.ExecutionReport.Iterations.Should().Be(2);
        result.ExecutionReport.ToolCalls.Should().ContainSingle(record =>
            record.Name == "search_tasks" && record.Success);
        result.Actions.Should().ContainSingle(action => action.Type == TaskAssistantActionTypes.Listed);
        result.AssistantMessage.Should().Be("All done.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenMaxIterationsExceeded()
    {
        var userId = Guid.NewGuid();

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

        var act = () => _sut.ExecuteAsync(
            new ExecutorAgentRequest(
                userId,
                [new AgentMessageDto("user", "Search tasks")],
                CreatePlan(),
                []));

        await act.Should().ThrowAsync<LlmException>()
            .WithMessage("*maximum execute iterations*");
    }

    private static AgentPlan CreatePlan() =>
        new(
            "Search pending tasks",
            [new AgentPlanStep(1, "Search pending tasks", "search_tasks")],
            false,
            "low");
}
