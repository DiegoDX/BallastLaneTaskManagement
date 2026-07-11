using System.Text.Json;
using Application.DTOs.Common;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.DTOs.Tasks;
using Application.Interfaces.Services;
using Application.Llm.TaskAssistant;
using Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Tests.Application.Llm.TaskAssistant;

public sealed class TaskToolExecutorTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider TimeProvider = new FakeTimeProvider(FixedUtcNow);

    private readonly Mock<ITaskService> _taskServiceMock = new();
    private readonly TaskToolExecutor _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public TaskToolExecutorTests()
    {
        _sut = new TaskToolExecutor(new TaskToolHandlers(_taskServiceMock.Object, TimeProvider));
    }

    [Fact]
    public async Task ExecuteAsync_create_task_succeeds_when_arguments_are_valid()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var toolCall = CreateToolCall(
            TaskToolNames.CreateTask,
            """{"title":"Buy milk","description":"2% organic","dueDate":"2026-07-10"}""");

        _taskServiceMock
            .Setup(service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskResponse(
                taskId,
                _userId,
                "Buy milk",
                "2% organic",
                "Pending",
                dueDate,
                FixedUtcNow));

        // Act
        var result = await _sut.ExecuteAsync(_userId, toolCall);

        // Assert
        result.Action.Should().NotBeNull();
        result.Action!.Type.Should().Be(TaskAssistantActionTypes.Created);
        result.Action.TaskId.Should().Be(taskId);
        result.Action.Title.Should().Be("Buy milk");
        result.Action.DueDate.Should().Be(dueDate);

        using var document = JsonDocument.Parse(result.ResultJson);
        document.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("taskId").GetGuid().Should().Be(taskId);
        document.RootElement.GetProperty("title").GetString().Should().Be("Buy milk");

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(
                It.Is<CreateTaskRequest>(request =>
                    request.Title == "Buy milk" &&
                    request.Description == "2% organic" &&
                    request.DueDate == dueDate &&
                    request.UserId == _userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_create_task_returns_error_when_title_is_empty()
    {
        var toolCall = CreateToolCall(
            TaskToolNames.CreateTask,
            """{"title":"   ","dueDate":"2026-07-10"}""");

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain("Title is required");

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_create_task_returns_error_when_due_date_is_invalid()
    {
        var toolCall = CreateToolCall(
            TaskToolNames.CreateTask,
            """{"title":"Buy milk","dueDate":"not-a-date"}""");

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain("valid dueDate");

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_create_task_returns_error_when_title_exceeds_max_length()
    {
        var title = new string('a', TaskTitle.MaxLength + 1);
        var toolCall = CreateToolCall(
            TaskToolNames.CreateTask,
            $$"""{"title":"{{title}}","dueDate":"2026-07-10"}""");

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain($"{TaskTitle.MaxLength}");

        _taskServiceMock.Verify(
            service => service.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_returns_error_for_unknown_tool()
    {
        var toolCall = CreateToolCall("unknown_tool", """{"title":"Test"}""");

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain("Unknown tool");
    }

    [Fact]
    public async Task ExecuteAsync_returns_error_when_arguments_are_invalid_json()
    {
        var toolCall = CreateToolCall(TaskToolNames.CreateTask, "not-json");

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain("could not be parsed");
    }

    [Fact]
    public async Task ExecuteAsync_list_tasks_succeeds_with_filters()
    {
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var toolCall = CreateToolCall(
            TaskToolNames.ListTasks,
            """{"status":"Pending","title":"milk","pageSize":5}""");

        var pagedResult = new PagedResult<TaskListItemResponse>(
            [
                new TaskListItemResponse(
                    taskId,
                    _userId,
                    "Buy milk",
                    null,
                    "Pending",
                    dueDate,
                    FixedUtcNow)
            ],
            1,
            5,
            1,
            1);

        _taskServiceMock
            .Setup(service => service.SearchTasksAsync(
                _userId,
                It.IsAny<TaskSearchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().NotBeNull();
        result.Action!.Type.Should().Be(TaskAssistantActionTypes.Listed);

        using var document = JsonDocument.Parse(result.ResultJson);
        document.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("totalRecords").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("tasks").GetArrayLength().Should().Be(1);

        _taskServiceMock.Verify(
            service => service.SearchTasksAsync(
                _userId,
                It.Is<TaskSearchRequest>(request =>
                    request.PageNumber == 1 &&
                    request.PageSize == 5 &&
                    request.Status == "Pending" &&
                    request.Title == "milk"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_list_tasks_returns_error_when_page_size_is_invalid()
    {
        var toolCall = CreateToolCall(TaskToolNames.ListTasks, """{"pageSize":0}""");

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain("pageSize");

        _taskServiceMock.Verify(
            service => service.SearchTasksAsync(
                It.IsAny<Guid>(),
                It.IsAny<TaskSearchRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_get_task_succeeds_when_task_id_is_valid()
    {
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var toolCall = CreateToolCall(
            TaskToolNames.GetTask,
            $$"""{"taskId":"{{taskId}}"}""");

        _taskServiceMock
            .Setup(service => service.GetTaskByIdAsync(taskId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskResponse(
                taskId,
                _userId,
                "Buy milk",
                "2% organic",
                "Pending",
                dueDate,
                FixedUtcNow));

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().NotBeNull();
        result.Action!.Type.Should().Be(TaskAssistantActionTypes.Listed);

        using var document = JsonDocument.Parse(result.ResultJson);
        document.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("tasks")[0].GetProperty("taskId").GetGuid().Should().Be(taskId);
        document.RootElement.GetProperty("tasks")[0].GetProperty("title").GetString().Should().Be("Buy milk");
    }

    [Fact]
    public async Task ExecuteAsync_get_task_returns_error_when_task_id_is_invalid()
    {
        var toolCall = CreateToolCall(TaskToolNames.GetTask, """{"taskId":"not-a-guid"}""");

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain("valid GUID");

        _taskServiceMock.Verify(
            service => service.GetTaskByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_update_task_succeeds_when_arguments_are_valid()
    {
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var toolCall = CreateToolCall(
            TaskToolNames.UpdateTask,
            $$"""{"taskId":"{{taskId}}","title":"Buy oat milk","status":"InProgress"}""");

        var existing = new TaskResponse(
            taskId,
            _userId,
            "Buy milk",
            "2% organic",
            "Pending",
            dueDate,
            FixedUtcNow);

        var updated = existing with { Title = "Buy oat milk", Status = "InProgress" };

        _taskServiceMock
            .SetupSequence(service => service.GetTaskByIdAsync(taskId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .ReturnsAsync(updated);

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().NotBeNull();
        result.Action!.Type.Should().Be(TaskAssistantActionTypes.Updated);
        result.Action.TaskId.Should().Be(taskId);
        result.Action.Title.Should().Be("Buy oat milk");
        result.Action.Status.Should().Be("InProgress");

        _taskServiceMock.Verify(
            service => service.UpdateTaskAsync(
                It.Is<UpdateTaskRequest>(request =>
                    request.TaskId == taskId &&
                    request.Title == "Buy oat milk" &&
                    request.Description == "2% organic" &&
                    request.Status == "InProgress"),
                _userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_update_task_returns_error_when_no_fields_provided()
    {
        var taskId = Guid.NewGuid();
        var toolCall = CreateToolCall(
            TaskToolNames.UpdateTask,
            $$"""{"taskId":"{{taskId}}"}""");

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain("At least one of title, description, or status");

        _taskServiceMock.Verify(
            service => service.UpdateTaskAsync(
                It.IsAny<UpdateTaskRequest>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_delete_task_succeeds_when_task_exists()
    {
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var toolCall = CreateToolCall(
            TaskToolNames.DeleteTask,
            $$"""{"taskId":"{{taskId}}"}""");

        _taskServiceMock
            .Setup(service => service.GetTaskByIdAsync(taskId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskResponse(
                taskId,
                _userId,
                "Buy milk",
                null,
                "Pending",
                dueDate,
                FixedUtcNow));

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().NotBeNull();
        result.Action!.Type.Should().Be(TaskAssistantActionTypes.Deleted);
        result.Action.TaskId.Should().Be(taskId);
        result.Action.Title.Should().Be("Buy milk");

        _taskServiceMock.Verify(
            service => service.DeleteTaskAsync(taskId, _userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_delete_task_returns_error_when_task_id_is_missing()
    {
        var toolCall = CreateToolCall(TaskToolNames.DeleteTask, "{}");

        var result = await _sut.ExecuteAsync(_userId, toolCall);

        result.Action.Should().BeNull();
        result.ResultJson.Should().Contain("taskId is required");

        _taskServiceMock.Verify(
            service => service.DeleteTaskAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static LlmToolCall CreateToolCall(string name, string arguments) =>
        new("call_1", name, arguments);

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTime _utcNow;

        public FakeTimeProvider(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => new(_utcNow);
    }
}
