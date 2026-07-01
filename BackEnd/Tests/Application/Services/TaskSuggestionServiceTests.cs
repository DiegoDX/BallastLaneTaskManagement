using Application.DTOs.Llm;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class TaskSuggestionServiceTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly TaskSuggestionService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public TaskSuggestionServiceTests()
    {
        _sut = new TaskSuggestionService(_llmClientMock.Object);
    }

    [Fact]
    public void ValidateLlmChatRequest_WhenMessagesEmpty_ThrowsValidationException()
    {
        var request = new LlmChatRequest([]);

        var act = () => TaskSuggestionService.ValidateLlmChatRequest(request);

        var exception = Assert.Throws<ValidationException>(act);
        Assert.Equal("At least one chat message is required.", exception.Message);
    }

    [Fact]
    public async Task SuggestAsync_WhenPromptEmpty_ThrowsValidationException()
    {
        var act = () => _sut.SuggestAsync(_userId, new TaskSuggestionRequest("   "));

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        Assert.Equal("Prompt is required.", exception.Message);
    }

    [Fact]
    public async Task SuggestAsync_WhenUserIdEmpty_ThrowsValidationException()
    {
        var act = () => _sut.SuggestAsync(Guid.Empty, new TaskSuggestionRequest("Draft release notes"));

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("User id is required.");
    }

    [Fact]
    public async Task SuggestAsync_WhenRequestIsNull_ThrowsValidationException()
    {
        var act = () => _sut.SuggestAsync(_userId, null!);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Task suggestion request is required.");
    }

    [Fact]
    public async Task SuggestAsync_ReturnsParsedSuggestion_WhenLlmReturnsValidJson()
    {
        // Arrange
        const string llmContent = """
            {"title":"Prepare Q2 report","description":"Include revenue breakdown"}
            """;

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(llmContent, "gpt-4o-mini"));

        // Act
        var result = await _sut.SuggestAsync(_userId, new TaskSuggestionRequest("Prepare Q2 financial report"));

        // Assert
        result.Should().BeEquivalentTo(new TaskSuggestionResponse(
            "Prepare Q2 report",
            "Include revenue breakdown"));

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(
                It.Is<LlmChatRequest>(request =>
                    request.Messages.Count == 2 &&
                    request.Messages[0].Role == LlmMessageRole.System &&
                    request.Messages[1].Content == "Prepare Q2 financial report"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuggestAsync_ThrowsValidationException_WhenLlmReturnsMalformedJson()
    {
        // Arrange
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse("not-json"));

        // Act
        var act = () => _sut.SuggestAsync(_userId, new TaskSuggestionRequest("Draft release notes"));

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Task suggestion response could not be parsed.");
    }

    [Fact]
    public async Task SuggestAsync_PropagatesLlmException_WhenLlmClientFails()
    {
        // Arrange
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmException("The LLM service is temporarily unavailable.", isTransient: true));

        // Act
        var act = () => _sut.SuggestAsync(_userId, new TaskSuggestionRequest("Draft release notes"));

        // Assert
        var exception = await Assert.ThrowsAsync<LlmException>(act);
        exception.IsTransient.Should().BeTrue();
        exception.Message.Should().Be("The LLM service is temporarily unavailable.");
    }
}
