using Application.DTOs.Chat;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using Application.Llm;
using Application.Services;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class ChatServiceTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly ChatService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public ChatServiceTests()
    {
        _sut = new ChatService(_llmClientMock.Object);
    }

    [Fact]
    public async Task ChatAsync_WhenUserIdEmpty_ThrowsValidationException()
    {
        var request = new ChatRequest([new ChatMessageDto("user", "Hello")]);

        var act = () => _sut.ChatAsync(Guid.Empty, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("User id is required.");
    }

    [Fact]
    public async Task ChatAsync_WhenRequestIsNull_ThrowsValidationException()
    {
        var act = () => _sut.ChatAsync(_userId, null!);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Chat request is required.");
    }

    [Fact]
    public async Task ChatAsync_WhenMessagesEmpty_ThrowsValidationException()
    {
        var request = new ChatRequest([]);

        var act = () => _sut.ChatAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("At least one chat message is required.");
    }

    [Fact]
    public async Task ChatAsync_WhenTooManyMessages_ThrowsValidationException()
    {
        var messages = Enumerable
            .Range(0, ChatLimits.MaxMessages + 1)
            .Select(index => new ChatMessageDto("user", $"Message {index}"))
            .ToList();

        var request = new ChatRequest(messages);

        var act = () => _sut.ChatAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be($"At most {ChatLimits.MaxMessages} messages are allowed.");
    }

    [Fact]
    public async Task ChatAsync_WhenRoleInvalid_ThrowsValidationException()
    {
        var request = new ChatRequest([new ChatMessageDto("system", "Hello")]);

        var act = () => _sut.ChatAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Message role must be 'user' or 'assistant'.");
    }

    [Fact]
    public async Task ChatAsync_WhenContentEmpty_ThrowsValidationException()
    {
        var request = new ChatRequest([new ChatMessageDto("user", "   ")]);

        var act = () => _sut.ChatAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Message content is required.");
    }

    [Fact]
    public async Task ChatAsync_ReturnsAssistantResponse_WhenLlmSucceeds()
    {
        // Arrange
        const string assistantContent = "Hello! How can I help you today?";

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(assistantContent, "gpt-4o-mini"));

        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "Hello")
        ]);

        // Act
        var result = await _sut.ChatAsync(_userId, request);

        // Assert
        result.Should().BeEquivalentTo(new ChatResponse(assistantContent, "gpt-4o-mini"));

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(
                It.Is<LlmChatRequest>(chatRequest =>
                    chatRequest.Messages.Count == 2 &&
                    chatRequest.Messages[0].Role == LlmMessageRole.System &&
                    chatRequest.Messages[1].Role == LlmMessageRole.User &&
                    chatRequest.Messages[1].Content == "Hello"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChatAsync_PreservesConversationHistory_WhenMultipleMessagesProvided()
    {
        // Arrange
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse("Sure, I can help with that.", "gpt-4o-mini"));

        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "What is Clean Architecture?"),
            new ChatMessageDto("assistant", "Clean Architecture separates concerns into layers."),
            new ChatMessageDto("user", "Can you give an example?")
        ]);

        // Act
        await _sut.ChatAsync(_userId, request);

        // Assert
        _llmClientMock.Verify(
            client => client.CompleteChatAsync(
                It.Is<LlmChatRequest>(chatRequest =>
                    chatRequest.Messages.Count == 4 &&
                    chatRequest.Messages[0].Role == LlmMessageRole.System &&
                    chatRequest.Messages[1].Role == LlmMessageRole.User &&
                    chatRequest.Messages[2].Role == LlmMessageRole.Assistant &&
                    chatRequest.Messages[3].Role == LlmMessageRole.User &&
                    chatRequest.Messages[3].Content == "Can you give an example?"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChatAsync_PropagatesLlmException_WhenLlmClientFails()
    {
        // Arrange
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmException("The LLM service is temporarily unavailable.", isTransient: true));

        var request = new ChatRequest([new ChatMessageDto("user", "Hello")]);

        // Act
        var act = () => _sut.ChatAsync(_userId, request);

        // Assert
        var exception = await Assert.ThrowsAsync<LlmException>(act);
        exception.IsTransient.Should().BeTrue();
    }

    [Theory]
    [InlineData("user", LlmMessageRole.User)]
    [InlineData("USER", LlmMessageRole.User)]
    [InlineData("assistant", LlmMessageRole.Assistant)]
    [InlineData("Assistant", LlmMessageRole.Assistant)]
    public void MapRole_MapsAllowedRoles(string role, LlmMessageRole expectedRole)
    {
        var result = ChatPromptBuilder.MapRole(role);

        result.Should().Be(expectedRole);
    }
}
