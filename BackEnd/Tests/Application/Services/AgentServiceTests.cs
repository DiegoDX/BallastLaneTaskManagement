using Application.Agent;
using Application.DTOs.Agent;
using Application.Exceptions;
using Application.Interfaces.Services;
using Application.Services;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class AgentServiceTests
{
    private readonly Mock<IAgentOrchestrator> _orchestratorMock = new();
    private readonly AgentService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public AgentServiceTests()
    {
        _sut = new AgentService(_orchestratorMock.Object);
    }

    [Fact]
    public async Task RunAsync_WhenUserIdEmpty_ThrowsValidationException()
    {
        var request = new AgentRequest([new AgentMessageDto("user", "Organize my tasks")]);

        var act = () => _sut.RunAsync(Guid.Empty, request);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Message.Should().Be("User id is required.");
    }

    [Fact]
    public async Task RunAsync_WhenMessagesEmpty_ThrowsValidationException()
    {
        var request = new AgentRequest([]);

        var act = () => _sut.RunAsync(_userId, request);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Message.Should().Be("At least one message is required.");
    }

    [Fact]
    public async Task ContinueAsync_WhenRunIdEmpty_ThrowsValidationException()
    {
        var request = new AgentContinueRequest(Guid.Empty, true);

        var act = () => _sut.ContinueAsync(_userId, request);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Message.Should().Be("Run id is required.");
    }

    [Fact]
    public async Task RunAsync_DelegatesToOrchestrator()
    {
        var request = new AgentRequest([new AgentMessageDto("user", "Organize my tasks")]);
        var expected = new AgentResponse(
            "Done",
            [],
            [],
            null,
            null,
            AgentRunStatus.Completed,
            null,
            "gpt-4o-mini");

        _orchestratorMock
            .Setup(orchestrator => orchestrator.RunAsync(_userId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.RunAsync(_userId, request);

        result.Should().Be(expected);
    }
}
