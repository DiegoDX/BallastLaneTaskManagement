using Application.DTOs.Common;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.DTOs.Tasks;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Llm.TaskAssistant;
using Application.Services;
using Domain.Enums;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class TaskPlanningServiceTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider TimeProvider = new FakeTimeProvider(FixedUtcNow);

    private readonly Mock<ITaskService> _taskServiceMock = new();
    private readonly Mock<ITaskAnalyticsService> _analyticsServiceMock = new();
    private readonly Mock<ITaskToolHandlers> _taskToolHandlersMock = new();
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly TaskPlanningService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public TaskPlanningServiceTests()
    {
        _sut = new TaskPlanningService(
            _taskServiceMock.Object,
            _analyticsServiceMock.Object,
            _taskToolHandlersMock.Object,
            _llmClientMock.Object,
            TimeProvider);
    }

    [Fact]
    public async Task SuggestNextAsync_ShouldReturnNearestOpenTaskByDueDate()
    {
        var laterTaskId = Guid.NewGuid();
        var soonerTaskId = Guid.NewGuid();

        _taskServiceMock
            .Setup(service => service.SearchTasksAsync(_userId, It.IsAny<TaskSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TaskListItemResponse>(
                [
                    CreateTask(laterTaskId, "Later task", TaskItemStatus.Pending, FixedUtcNow.Date.AddDays(3)),
                    CreateTask(soonerTaskId, "Sooner task", TaskItemStatus.InProgress, FixedUtcNow.Date.AddDays(1))
                ],
                1,
                50,
                2,
                1));

        var suggestion = await _sut.SuggestNextAsync(_userId);

        suggestion.TaskId.Should().Be(soonerTaskId);
        suggestion.Title.Should().Be("Sooner task");
        suggestion.Status.Should().Be(TaskItemStatus.InProgress.ToString());
    }

    [Fact]
    public async Task SummarizeProgressAsync_ShouldIncludeStatisticsInSummary()
    {
        var statistics = new TaskStatisticsResponse(5, 2, 1, 2, 1, 1);

        _analyticsServiceMock
            .Setup(service => service.GetStatisticsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(statistics);

        var response = await _sut.SummarizeProgressAsync(_userId);

        response.Statistics.Should().Be(statistics);
        response.Summary.Should().Contain("5 tasks");
        response.Summary.Should().Contain("2 pending");
    }

    [Fact]
    public async Task PrioritizeAsync_ShouldOrderOpenTasksByDueDate()
    {
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();

        _taskServiceMock
            .Setup(service => service.SearchTasksAsync(_userId, It.IsAny<TaskSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TaskListItemResponse>(
                [
                    CreateTask(secondTaskId, "Second", TaskItemStatus.Pending, FixedUtcNow.Date.AddDays(5)),
                    CreateTask(firstTaskId, "First", TaskItemStatus.Pending, FixedUtcNow.Date.AddDays(1))
                ],
                1,
                50,
                2,
                1));

        var response = await _sut.PrioritizeAsync(_userId);

        response.Tasks.Should().HaveCount(2);
        response.Tasks[0].TaskId.Should().Be(firstTaskId);
        response.Tasks[1].TaskId.Should().Be(secondTaskId);
        response.Tasks[0].PriorityRank.Should().Be(1);
    }

    private TaskListItemResponse CreateTask(
        Guid taskId,
        string title,
        TaskItemStatus status,
        DateTime dueDate) =>
        new(taskId, _userId, title, null, status.ToString(), dueDate, FixedUtcNow);

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
