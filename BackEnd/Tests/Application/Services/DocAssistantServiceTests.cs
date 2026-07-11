using Application.DTOs.DocAssistant;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using Application.Rag;
using Application.Services;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class DocAssistantServiceTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly Mock<IRagRetriever> _retrieverMock = new();
    private readonly Mock<IDocumentIndexer> _documentIndexerMock = new();
    private readonly DocAssistantService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public DocAssistantServiceTests()
    {
        _sut = new DocAssistantService(
            _llmClientMock.Object,
            _retrieverMock.Object,
            _documentIndexerMock.Object);
    }

    [Fact]
    public async Task AskAsync_WhenUserIdEmpty_ThrowsValidationException()
    {
        var request = new DocAssistantRequest([new DocAssistantMessageDto("user", "Hello")]);

        var act = () => _sut.AskAsync(Guid.Empty, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("User id is required.");
    }

    [Fact]
    public async Task AskAsync_WhenRequestIsNull_ThrowsValidationException()
    {
        var act = () => _sut.AskAsync(_userId, null!);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Doc assistant request is required.");
    }

    [Fact]
    public async Task AskAsync_WhenMessagesEmpty_ThrowsValidationException()
    {
        var request = new DocAssistantRequest([]);

        var act = () => _sut.AskAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("At least one message is required.");
    }

    [Fact]
    public async Task AskAsync_WhenTooManyMessages_ThrowsValidationException()
    {
        var messages = Enumerable
            .Range(0, DocAssistantLimits.MaxMessages + 1)
            .Select(index => new DocAssistantMessageDto("user", $"Message {index}"))
            .ToList();

        var request = new DocAssistantRequest(messages);

        var act = () => _sut.AskAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be($"At most {DocAssistantLimits.MaxMessages} messages are allowed.");
    }

    [Fact]
    public async Task AskAsync_WhenRoleInvalid_ThrowsValidationException()
    {
        var request = new DocAssistantRequest([new DocAssistantMessageDto("system", "Hello")]);

        var act = () => _sut.AskAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Message role must be 'user' or 'assistant'.");
    }

    [Fact]
    public async Task AskAsync_ReturnsResponseWithMappedSources_WhenLlmSucceeds()
    {
        const string question = "How does authentication work?";
        const string assistantContent = "Authentication uses JWT Bearer tokens.";
        var chunks = new[]
        {
            CreateChunk("README.md", 5, "Authentication uses JWT Bearer tokens issued after login."),
            CreateChunk("Requirements.docx", 2, "All protected endpoints require a valid JWT.")
        };

        _retrieverMock
            .Setup(retriever => retriever.RetrieveAsync(question, DocAssistantLimits.TopK, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(assistantContent, "llama3.2"));

        var request = new DocAssistantRequest([new DocAssistantMessageDto("user", question)]);

        var result = await _sut.AskAsync(_userId, request);

        result.Content.Should().Be(assistantContent);
        result.Model.Should().Be("llama3.2");
        result.Sources.Should().HaveCount(2);
        result.Sources[0].Should().BeEquivalentTo(new DocAssistantSource(
            "README.md",
            5,
            "Authentication uses JWT Bearer tokens issued after login."));
        result.Sources[1].Should().BeEquivalentTo(new DocAssistantSource(
            "Requirements.docx",
            2,
            "All protected endpoints require a valid JWT."));

        _retrieverMock.Verify(
            retriever => retriever.RetrieveAsync(question, DocAssistantLimits.TopK, It.IsAny<CancellationToken>()),
            Times.Once);

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(
                It.Is<LlmChatRequest>(chatRequest =>
                    chatRequest.Messages.Count == 2 &&
                    chatRequest.Messages[0].Role == LlmMessageRole.System &&
                    chatRequest.Messages[0].Content.Contains("[README.md chunk 5]") &&
                    chatRequest.Messages[1].Role == LlmMessageRole.User &&
                    chatRequest.Messages[1].Content == question &&
                    chatRequest.Temperature == 0.2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AskAsync_UsesLastUserMessageForRetrieval_WhenConversationHasHistory()
    {
        const string latestQuestion = "Which layers exist?";

        _retrieverMock
            .Setup(retriever => retriever.RetrieveAsync(latestQuestion, DocAssistantLimits.TopK, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DocumentChunk>());

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse("Domain, Application, Infrastructure, and API.", "llama3.2"));

        var request = new DocAssistantRequest(
        [
            new DocAssistantMessageDto("user", "What is the project architecture?"),
            new DocAssistantMessageDto("assistant", "It follows Clean Architecture."),
            new DocAssistantMessageDto("user", latestQuestion)
        ]);

        await _sut.AskAsync(_userId, request);

        _retrieverMock.Verify(
            retriever => retriever.RetrieveAsync(latestQuestion, DocAssistantLimits.TopK, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AskAsync_TruncatesSourceExcerptsToConfiguredLength()
    {
        var longContent = new string('A', DocAssistantLimits.ExcerptLength + 50);
        var chunks = new[] { CreateChunk("README.md", 0, longContent) };

        _retrieverMock
            .Setup(retriever => retriever.RetrieveAsync(It.IsAny<string>(), DocAssistantLimits.TopK, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse("Answer", "llama3.2"));

        var request = new DocAssistantRequest([new DocAssistantMessageDto("user", "Question")]);

        var result = await _sut.AskAsync(_userId, request);

        result.Sources.Should().ContainSingle();
        result.Sources[0].Excerpt.Should().HaveLength(DocAssistantLimits.ExcerptLength);
        result.Sources[0].Excerpt.Should().Be(longContent[..DocAssistantLimits.ExcerptLength]);
    }

    [Fact]
    public async Task AskAsync_PropagatesLlmException_WhenLlmClientFails()
    {
        _retrieverMock
            .Setup(retriever => retriever.RetrieveAsync(It.IsAny<string>(), DocAssistantLimits.TopK, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DocumentChunk>());

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmException("The LLM service is temporarily unavailable.", isTransient: true));

        var request = new DocAssistantRequest([new DocAssistantMessageDto("user", "Hello")]);

        var act = () => _sut.AskAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<LlmException>(act);
        exception.IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task ReindexAsync_DelegatesToDocumentIndexer()
    {
        _documentIndexerMock
            .Setup(indexer => indexer.IndexAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.ReindexAsync();

        _documentIndexerMock.Verify(
            indexer => indexer.IndexAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static DocumentChunk CreateChunk(string sourceFile, int chunkIndex, string content) =>
        new($"{sourceFile}-{chunkIndex}", sourceFile, chunkIndex, content, [1f, 0f, 0f]);
}
