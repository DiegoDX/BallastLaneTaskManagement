using Application.Agent;
using Application.Agent.Specialists;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests.Application.Agent.Specialists;

public sealed class PlannerAgentTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly PlannerAgent _sut;

    public PlannerAgentTests()
    {
        _sut = new PlannerAgent(
            _llmClientMock.Object,
            Options.Create(new AgentOptions { BulkUpdateApprovalThreshold = 3 }));
    }

    [Fact]
    public async Task PlanAsync_ShouldRequireApproval_WhenPlanIncludesDeleteTask()
    {
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(
                """
                {
                  "goal": "Delete old tasks",
                  "steps": [
                    { "order": 1, "description": "Delete task", "toolHint": "delete_task" }
                  ],
                  "requiresApproval": false,
                  "riskLevel": "low"
                }
                """,
                "gpt-test"));

        var result = await _sut.PlanAsync(
            new PlannerAgentRequest(Guid.NewGuid(), [new AgentMessageDto("user", "Delete old tasks")]));

        result.Plan.RequiresApproval.Should().BeTrue();
        result.Model.Should().Be("gpt-test");
    }

    [Fact]
    public async Task PlanAsync_ShouldThrow_WhenResponseCannotBeParsed()
    {
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse("not-json", "gpt-test"));

        var act = () => _sut.PlanAsync(
            new PlannerAgentRequest(Guid.NewGuid(), [new AgentMessageDto("user", "Plan tasks")]));

        await act.Should().ThrowAsync<ValidationException>();
    }
}
