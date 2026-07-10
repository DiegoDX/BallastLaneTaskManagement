using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.Exceptions;
using Application.Interfaces;
using Application.Llm.TaskAssistant;
using Application.Services;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class TaskAssistantServiceTests
{
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly Mock<ITaskToolExecutor> _toolExecutorMock = new();
    private readonly TaskAssistantService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public TaskAssistantServiceTests()
    {
        _sut = new TaskAssistantService(_llmClientMock.Object, _toolExecutorMock.Object);
    }

    [Fact]
    public async Task AssistAsync_WhenUserIdEmpty_ThrowsValidationException()
    {
        var request = new TaskAssistantRequest([new TaskAssistantMessageDto("user", "Hello")]);

        var act = () => _sut.AssistAsync(Guid.Empty, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("User id is required.");
    }

    [Fact]
    public async Task AssistAsync_WhenRequestIsNull_ThrowsValidationException()
    {
        var act = () => _sut.AssistAsync(_userId, null!);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Task assistant request is required.");
    }

    [Fact]
    public async Task AssistAsync_WhenMessagesEmpty_ThrowsValidationException()
    {
        var request = new TaskAssistantRequest([]);

        var act = () => _sut.AssistAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("At least one message is required.");
    }

    [Fact]
    public async Task AssistAsync_WhenTooManyMessages_ThrowsValidationException()
    {
        var messages = Enumerable
            .Range(0, TaskAssistantLimits.MaxMessages + 1)
            .Select(index => new TaskAssistantMessageDto("user", $"Message {index}"))
            .ToList();

        var request = new TaskAssistantRequest(messages);

        var act = () => _sut.AssistAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be($"At most {TaskAssistantLimits.MaxMessages} messages are allowed.");
    }

    [Fact]
    public async Task AssistAsync_WhenRoleInvalid_ThrowsValidationException()
    {
        var request = new TaskAssistantRequest([new TaskAssistantMessageDto("system", "Hello")]);

        var act = () => _sut.AssistAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<ValidationException>(act);
        exception.Message.Should().Be("Message role must be 'user' or 'assistant'.");
    }

    [Fact]
    public async Task AssistAsync_ReturnsDirectResponse_WhenLlmReturnsContentWithoutToolCalls()
    {
        const string assistantContent = "What task would you like to create?";

        _llmClientMock
            .Setup(client => client.CompleteChatWithToolsAsync(
                It.IsAny<LlmChatRequest>(),
                It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatCompletion(assistantContent, [], "llama3.2"));

        var request = new TaskAssistantRequest([new TaskAssistantMessageDto("user", "Hello")]);

        var result = await _sut.AssistAsync(_userId, request);

        result.Content.Should().Be(assistantContent);
        result.Model.Should().Be("llama3.2");
        result.Actions.Should().BeEmpty();

        _llmClientMock.Verify(
            client => client.CompleteChatWithToolsAsync(
                It.Is<LlmChatRequest>(chatRequest =>
                    chatRequest.Messages.Count == 2 &&
                    chatRequest.Messages[0].Role == LlmMessageRole.System &&
                    chatRequest.Messages[1].Role == LlmMessageRole.User &&
                    chatRequest.Messages[1].Content == "Hello"),
                It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _toolExecutorMock.Verify(
            executor => executor.ExecuteAsync(
                It.IsAny<Guid>(),
                It.IsAny<LlmToolCall>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AssistAsync_ExecutesToolCallAndReturnsFinalResponse_AfterSecondLlmTurn()
    {
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var toolCall = new LlmToolCall(
            "call_1",
            TaskToolNames.CreateTask,
            """{"title":"Buy milk","dueDate":"2026-07-10"}""");

        var action = new TaskAssistantAction(
            TaskAssistantActionTypes.Created,
            taskId,
            "Buy milk",
            DueDate: dueDate);

        _llmClientMock
            .SetupSequence(client => client.CompleteChatWithToolsAsync(
                It.IsAny<LlmChatRequest>(),
                It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatCompletion(string.Empty, [toolCall], "llama3.2"))
            .ReturnsAsync(new LlmChatCompletion("Listo, creé la tarea Buy milk para mañana.", [], "llama3.2"));

        _toolExecutorMock
            .Setup(executor => executor.ExecuteAsync(_userId, toolCall, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskToolExecutionResult(
                """{"success":true,"taskId":"00000000-0000-0000-0000-000000000001","title":"Buy milk"}""",
                action));

        var request = new TaskAssistantRequest(
        [
            new TaskAssistantMessageDto("user", "creame una task Buy milk due tomorrow")
        ]);

        var result = await _sut.AssistAsync(_userId, request);

        result.Content.Should().Be("Listo, creé la tarea Buy milk para mañana.");
        result.Model.Should().Be("llama3.2");
        result.Actions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(action);

        _llmClientMock.Verify(
            client => client.CompleteChatWithToolsAsync(
                It.IsAny<LlmChatRequest>(),
                It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _toolExecutorMock.Verify(
            executor => executor.ExecuteAsync(_userId, toolCall, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AssistAsync_ThrowsLlmException_WhenMaxIterationsAreExceeded()
    {
        var toolCall = new LlmToolCall(
            "call_1",
            TaskToolNames.CreateTask,
            """{"title":"Buy milk","dueDate":"2026-07-10"}""");

        _llmClientMock
            .Setup(client => client.CompleteChatWithToolsAsync(
                It.IsAny<LlmChatRequest>(),
                It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatCompletion(string.Empty, [toolCall], "llama3.2"));

        _toolExecutorMock
            .Setup(executor => executor.ExecuteAsync(
                It.IsAny<Guid>(),
                It.IsAny<LlmToolCall>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskToolExecutionResult("""{"success":true}"""));

        var request = new TaskAssistantRequest([new TaskAssistantMessageDto("user", "Create a task")]);

        var act = () => _sut.AssistAsync(_userId, request);

        var exception = await Assert.ThrowsAsync<LlmException>(act);
        exception.Message.Should().Contain("maximum agent iterations");
        exception.IsTransient.Should().BeFalse();

        _llmClientMock.Verify(
            client => client.CompleteChatWithToolsAsync(
                It.IsAny<LlmChatRequest>(),
                It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(TaskAssistantLimits.MaxAgentIterations));
    }
}
