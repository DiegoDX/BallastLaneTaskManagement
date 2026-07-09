using Application.DTOs.Llm;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Services;
using Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class TaskSuggestionServiceTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly Mock<ITaskService> _taskServiceMock = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly TaskSuggestionService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public TaskSuggestionServiceTests()
    {
        _sut = new TaskSuggestionService(
            _llmClientMock.Object,
            _taskServiceMock.Object,
            _timeProvider);
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

    [Fact]
    public async Task CreateFromSuggestionsAsync_CreatesTasksFromRequestWithoutCallingLlm()
    {
        // Arrange
        var request = new TaskSuggestionCreateRequest(
        [
            new TaskSuggestionBatchItem("Review invoices", "Pending only"),
            new TaskSuggestionBatchItem("Send report", "To finance team")
        ]);

        _taskServiceMock
            .SetupSequence(service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTaskResponse("Review invoices", "Pending only", DateTime.UtcNow.AddDays(5)))
            .ReturnsAsync(CreateTaskResponse("Send report", "To finance team", DateTime.UtcNow.AddDays(10)));

        // Act
        var result = await _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        result.Should().HaveCount(2);
        result.Select(task => task.Title).Should().Equal("Review invoices", "Send report");

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(
                It.Is<CreateTaskRequest>(createRequest =>
                    createRequest.Title == "Review invoices" &&
                    createRequest.Description == "Pending only" &&
                    createRequest.UserId == _userId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(
                It.Is<CreateTaskRequest>(createRequest =>
                    createRequest.Title == "Send report" &&
                    createRequest.Description == "To finance team" &&
                    createRequest.UserId == _userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_NormalizesEmptyDescriptionToEmptyString()
    {
        // Arrange
        var request = new TaskSuggestionCreateRequest(
        [
            new TaskSuggestionBatchItem("Manual task", "   ")
        ]);

        _taskServiceMock
            .Setup(service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTaskResponse("Manual task", string.Empty, DateTime.UtcNow.AddDays(3)));

        // Act
        var result = await _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        result.Should().ContainSingle();

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(
                It.Is<CreateTaskRequest>(createRequest => createRequest.Description == string.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_ThrowsValidationException_WhenRequestIsNull()
    {
        // Act
        var act = () => _sut.CreateFromSuggestionsAsync(_userId, null!);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Task suggestion create request is required.");

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_ThrowsValidationException_WhenTasksIsNull()
    {
        // Arrange
        var request = new TaskSuggestionCreateRequest(Tasks: null);

        // Act
        var act = () => _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Task suggestion batch must contain at least one task.");

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_ThrowsValidationException_WhenTasksIsEmpty()
    {
        // Arrange
        var request = new TaskSuggestionCreateRequest([]);

        // Act
        var act = () => _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Task suggestion batch must contain at least one task.");
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_ThrowsValidationException_WhenTasksExceedMaxBatchSize()
    {
        // Arrange
        var tasks = Enumerable.Range(1, TaskSuggestionLimits.MaxBatchSize + 1)
            .Select(index => new TaskSuggestionBatchItem($"Task {index}", string.Empty))
            .ToList();

        var request = new TaskSuggestionCreateRequest(tasks);

        // Act
        var act = () => _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be(
            $"Task suggestion batch must contain at most {TaskSuggestionLimits.MaxBatchSize} tasks.");
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_ThrowsValidationException_WhenTitleIsMissing()
    {
        // Arrange
        var request = new TaskSuggestionCreateRequest(
        [
            new TaskSuggestionBatchItem("   ", "Details")
        ]);

        // Act
        var act = () => _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Title is required.");
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_ThrowsValidationException_WhenTitleTooLong()
    {
        // Arrange
        var longTitle = new string('a', TaskTitle.MaxLength + 1);
        var request = new TaskSuggestionCreateRequest(
        [
            new TaskSuggestionBatchItem(longTitle, "Details")
        ]);

        // Act
        var act = () => _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Title must be at most {MaxLength} characters long.");
    }

    [Fact]
    public async Task GenerateBatchAsync_WhenPromptEmpty_ThrowsValidationException()
    {
        var act = () => _sut.GenerateBatchAsync(
            _userId,
            new TaskSuggestionGenerateRequest("   "));

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Prompt is required.");
    }

    [Fact]
    public async Task GenerateBatchAsync_WhenUserIdEmpty_ThrowsValidationException()
    {
        var act = () => _sut.GenerateBatchAsync(
            Guid.Empty,
            new TaskSuggestionGenerateRequest("Plan onboarding"));

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("User id is required.");
    }

    [Fact]
    public async Task GenerateBatchAsync_WhenRequestIsNull_ThrowsValidationException()
    {
        var act = () => _sut.GenerateBatchAsync(_userId, null!);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Task suggestion generate request is required.");
    }

    [Fact]
    public async Task GenerateBatchAsync_ReturnsWrappedBatchResponse_WhenLlmReturnsValidJson()
    {
        // Arrange
        const string llmContent = """
            {
              "tasks": [
                {"title":"Review invoices","description":"Pending only"},
                {"title":"Send report","description":"To finance team"}
              ]
            }
            """;

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(llmContent, "gpt-4o-mini"));

        // Act
        var result = await _sut.GenerateBatchAsync(
            _userId,
            new TaskSuggestionGenerateRequest("Close Q2 books"));

        // Assert
        result.Tasks.Should().HaveCount(2);
        result.Tasks.Should().Equal(
            new TaskSuggestionBatchItem("Review invoices", "Pending only"),
            new TaskSuggestionBatchItem("Send report", "To finance team"));

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(
                It.Is<LlmChatRequest>(chatRequest =>
                    chatRequest.Messages.Count == 2 &&
                    chatRequest.Messages[0].Role == LlmMessageRole.System &&
                    chatRequest.Messages[1].Content == "Close Q2 books"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateBatchAsync_ThrowsValidationException_WhenLlmReturnsEmptyBatch()
    {
        // Arrange
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse("""{"tasks":[]}"""));

        // Act
        var act = () => _sut.GenerateBatchAsync(
            _userId,
            new TaskSuggestionGenerateRequest("Plan onboarding"));

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Task suggestion batch must contain at least one task.");
    }

    [Fact]
    public async Task GenerateBatchAsync_ThrowsValidationException_WhenLlmReturnsMalformedJson()
    {
        // Arrange
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse("not-json"));

        // Act
        var act = () => _sut.GenerateBatchAsync(
            _userId,
            new TaskSuggestionGenerateRequest("Plan onboarding"));

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Task suggestion response could not be parsed.");

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateBatchAsync_PropagatesLlmException_WhenLlmClientFails()
    {
        // Arrange
        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmException("The LLM service is temporarily unavailable.", isTransient: true));

        // Act
        var act = () => _sut.GenerateBatchAsync(
            _userId,
            new TaskSuggestionGenerateRequest("Plan onboarding"));

        // Assert
        var exception = await Assert.ThrowsAsync<LlmException>(act);
        exception.IsTransient.Should().BeTrue();
        exception.Message.Should().Be("The LLM service is temporarily unavailable.");
    }

    private TaskResponse CreateTaskResponse(string title, string description, DateTime dueDate) =>
        new(
            Guid.NewGuid(),
            _userId,
            title,
            description,
            "Pending",
            dueDate,
            DateTime.UtcNow);
}
