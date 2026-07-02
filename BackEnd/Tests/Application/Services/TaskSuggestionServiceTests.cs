using Application.DTOs.Llm;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Services;
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
    public async Task CreateFromSuggestionsAsync_CreatesSingleTaskFromLlmSuggestion()
    {
        // Arrange
        const string llmContent = """
            {"tasks":[{"title":"Prepare Q2 report","description":"Include revenue breakdown"}]}
            """;

        var rootDueDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var request = new TaskSuggestionCreateRequest(
            "Prepare Q2 financial report",
            TaskCount: 1,
            rootDueDate,
            Tasks: null);

        var createdTask = CreateTaskResponse("Prepare Q2 report", "Include revenue breakdown", rootDueDate);

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(llmContent, "gpt-4o-mini"));

        _taskServiceMock
            .Setup(service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTask);

        // Act
        var result = await _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        result.Should().ContainSingle().Which.Should().BeEquivalentTo(createdTask);

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(
                It.Is<LlmChatRequest>(chatRequest =>
                    chatRequest.Messages.Count == 2 &&
                    chatRequest.Messages[0].Role == LlmMessageRole.System &&
                    chatRequest.Messages[1].Content == "Prepare Q2 financial report"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(
                It.Is<CreateTaskRequest>(createRequest =>
                    createRequest.Title == "Prepare Q2 report" &&
                    createRequest.Description == "Include revenue breakdown" &&
                    createRequest.DueDate == rootDueDate &&
                    createRequest.UserId == _userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_CreatesBatchTasksFromSingleLlmCall()
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

        var rootDueDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var request = new TaskSuggestionCreateRequest(
            "Close Q2 books",
            TaskCount: 2,
            rootDueDate,
            Tasks: null);

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(llmContent, "gpt-4o-mini"));

        _taskServiceMock
            .SetupSequence(service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTaskResponse("Review invoices", "Pending only", rootDueDate))
            .ReturnsAsync(CreateTaskResponse("Send report", "To finance team", rootDueDate));

        // Act
        var result = await _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        result.Should().HaveCount(2);
        result.Select(task => task.Title).Should().Equal("Review invoices", "Send report");

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_UsesManualOverrideForFirstSlotAndLlmForSecond()
    {
        // Arrange
        const string llmContent = """
            {"tasks":[{"title":"Draft release notes","description":"Include breaking changes"}]}
            """;

        var rootDueDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var manualDueDate = new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        var request = new TaskSuggestionCreateRequest(
            "Complete release v1.2",
            TaskCount: 2,
            rootDueDate,
            [
                new TaskSuggestionTaskOverride("Tag release in GitHub", "Use annotated tag", manualDueDate)
            ]);

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse(llmContent, "gpt-4o-mini"));

        _taskServiceMock
            .SetupSequence(service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTaskResponse("Tag release in GitHub", "Use annotated tag", manualDueDate))
            .ReturnsAsync(CreateTaskResponse("Draft release notes", "Include breaking changes", rootDueDate));

        // Act
        var result = await _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Tag release in GitHub");
        result[1].Title.Should().Be("Draft release notes");

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(
                It.Is<CreateTaskRequest>(createRequest => createRequest.DueDate == manualDueDate),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(
                It.Is<CreateTaskRequest>(createRequest => createRequest.DueDate == rootDueDate),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_CreatesAllManualTasksWithoutCallingLlm()
    {
        // Arrange
        var rootDueDate = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
        var request = new TaskSuggestionCreateRequest(
            Prompt: null,
            TaskCount: 2,
            rootDueDate,
            [
                new TaskSuggestionTaskOverride("Manual task one", "First details", null),
                new TaskSuggestionTaskOverride("Manual task two", null, null)
            ]);

        _taskServiceMock
            .SetupSequence(service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTaskResponse("Manual task one", "First details", rootDueDate))
            .ReturnsAsync(CreateTaskResponse("Manual task two", string.Empty, rootDueDate));

        // Act
        var result = await _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        result.Should().HaveCount(2);
        result.Select(task => task.Title).Should().Equal("Manual task one", "Manual task two");

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_ThrowsValidationException_WhenPromptRequiredForLlmSlots()
    {
        // Arrange
        var request = new TaskSuggestionCreateRequest(
            Prompt: "   ",
            TaskCount: 2,
            DueDate: null,
            Tasks: null);

        // Act
        var act = () => _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Prompt is required.");

        _llmClientMock.Verify(
            client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_ThrowsValidationException_WhenTaskCountIsInvalid()
    {
        // Arrange
        var request = new TaskSuggestionCreateRequest("Plan sprint", TaskCount: 0, DueDate: null, Tasks: null);

        // Act
        var act = () => _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be($"Task count must be between 1 and {TaskSuggestionLimits.MaxBatchSize}.");
    }

    [Fact]
    public async Task CreateFromSuggestionsAsync_PropagatesLlmException_WhenBatchLlmCallFails()
    {
        // Arrange
        var request = new TaskSuggestionCreateRequest("Plan onboarding", TaskCount: 2, DueDate: null, Tasks: null);

        _llmClientMock
            .Setup(client => client.CompleteChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LlmException("The LLM service returned an invalid response.", isTransient: false));

        // Act
        var act = () => _sut.CreateFromSuggestionsAsync(_userId, request);

        // Assert
        var exception = await Assert.ThrowsAsync<LlmException>(act);
        exception.IsTransient.Should().BeFalse();

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
