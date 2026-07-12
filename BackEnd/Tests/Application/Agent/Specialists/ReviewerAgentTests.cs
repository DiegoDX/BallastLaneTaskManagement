using Application.Agent;
using Application.Agent.Specialists;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests.Application.Agent.Specialists;

public sealed class ReviewerAgentTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly ReviewerAgent _sut;

    public ReviewerAgentTests()
    {
        _sut = new ReviewerAgent(_llmClientMock.Object, Options.Create(new AgentOptions()));
    }

    [Fact]
    public async Task ReviewAsync_ShouldParseRequiresReExecutionFields()
    {
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(
                """
                {
                  "success": false,
                  "issues": ["Task update failed"],
                  "recommendations": ["Retry update_task"],
                  "requiresReExecution": true,
                  "reExecutionHint": "Retry update_task for the overdue items."
                }
                """,
                "gpt-test"));

        var result = await _sut.ReviewAsync(
            new ReviewerAgentRequest(
                new AgentPlan("Fix tasks", [], false, "low"),
                new AgentExecutionReport(1, [new AgentToolCallRecord("update_task", false)]),
                []));

        result.Review.Success.Should().BeFalse();
        result.Review.RequiresReExecution.Should().BeTrue();
        result.Review.ReExecutionHint.Should().Be("Retry update_task for the overdue items.");
        result.Model.Should().Be("gpt-test");
    }
}
