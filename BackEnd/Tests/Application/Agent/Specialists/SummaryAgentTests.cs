using Application.Agent;
using Application.Agent.Specialists;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;
using Application.DTOs.Llm;
using Application.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests.Application.Agent.Specialists;

public sealed class SummaryAgentTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly SummaryAgent _sut;

    public SummaryAgentTests()
    {
        _sut = new SummaryAgent(_llmClientMock.Object, Options.Create(new AgentOptions()));
    }

    [Fact]
    public async Task SummarizeAsync_ShouldReturnParsedSummary()
    {
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(
                """{"summary":"I updated 2 tasks and listed the rest."}""",
                "gpt-test"));

        var result = await _sut.SummarizeAsync(
            new SummaryAgentRequest(
                new AgentPlan("Organize tasks", [], false, "low"),
                new AgentReview(true, [], []),
                [],
                AgentRunStatus.Completed));

        result.Summary.Should().Be("I updated 2 tasks and listed the rest.");
        result.Model.Should().Be("gpt-test");
    }
}
