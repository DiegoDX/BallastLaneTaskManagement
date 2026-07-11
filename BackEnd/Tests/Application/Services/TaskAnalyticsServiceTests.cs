using Application.DTOs.Common;
using Application.DTOs.Tasks;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Enums;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class TaskAnalyticsServiceTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider TimeProvider = new FakeTimeProvider(FixedUtcNow);

    private readonly Mock<ITaskService> _taskServiceMock = new();
    private readonly TaskAnalyticsService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public TaskAnalyticsServiceTests()
    {
        _sut = new TaskAnalyticsService(_taskServiceMock.Object, TimeProvider);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldCountStatusesOverdueAndDueToday()
    {
        var tasks = new PagedResult<TaskListItemResponse>(
            [
                CreateTask(TaskItemStatus.Pending, FixedUtcNow.Date.AddDays(-1)),
                CreateTask(TaskItemStatus.InProgress, FixedUtcNow.Date),
                CreateTask(TaskItemStatus.Completed, FixedUtcNow.Date.AddDays(2)),
                CreateTask(TaskItemStatus.Pending, FixedUtcNow.Date)
            ],
            1,
            100,
            4,
            1);

        _taskServiceMock
            .Setup(service => service.SearchTasksAsync(_userId, It.IsAny<TaskSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        var statistics = await _sut.GetStatisticsAsync(_userId);

        statistics.Total.Should().Be(4);
        statistics.Pending.Should().Be(2);
        statistics.InProgress.Should().Be(1);
        statistics.Completed.Should().Be(1);
        statistics.Overdue.Should().Be(1);
        statistics.DueToday.Should().Be(2);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnZeros_WhenUserHasNoTasks()
    {
        _taskServiceMock
            .Setup(service => service.SearchTasksAsync(_userId, It.IsAny<TaskSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TaskListItemResponse>([], 1, 100, 0, 0));

        var statistics = await _sut.GetStatisticsAsync(_userId);

        statistics.Total.Should().Be(0);
        statistics.Pending.Should().Be(0);
        statistics.InProgress.Should().Be(0);
        statistics.Completed.Should().Be(0);
        statistics.Overdue.Should().Be(0);
        statistics.DueToday.Should().Be(0);
    }

    private TaskListItemResponse CreateTask(TaskItemStatus status, DateTime dueDate) =>
        new(
            Guid.NewGuid(),
            _userId,
            $"Task {status}",
            null,
            status.ToString(),
            dueDate,
            FixedUtcNow);

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
