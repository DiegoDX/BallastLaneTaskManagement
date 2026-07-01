using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Exceptions;

namespace Tests.Infrastructure.Repositories;

[Collection("DatabaseIntegration")]
[Trait("Category", "Integration")]
public sealed class TaskRepositoryTests : IAsyncLifetime
{
    private readonly IntegrationDatabaseFixture _fixture;
    private readonly List<Guid> _createdUserIds = [];
    private readonly List<Guid> _createdTaskIds = [];

    public TaskRepositoryTests(IntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var taskId in _createdTaskIds)
        {
            await _fixture.CleanupTaskAsync(taskId);
        }

        foreach (var userId in _createdUserIds)
        {
            await _fixture.CleanupUserAsync(userId);
        }
    }

    [Fact]
    public async Task CreateAsync_inserts_a_task_with_expected_field_values()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

        var createdAtUtc = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

        var task = TaskItem.Create(
            taskId,
            user.Id,
            "Write integration tests",
            dueDate,
            createdAtUtc,
            "Cover repository CRUD",
            TaskItemStatus.InProgress);

        _createdTaskIds.Add(taskId);

        // Act
        await _fixture.TaskRepository.CreateAsync(task);
        var loadedTask = await _fixture.TaskRepository.GetByIdAsync(taskId);

        // Assert
        loadedTask.Should().NotBeNull();
        loadedTask!.Id.Should().Be(taskId);
        loadedTask.UserId.Should().Be(user.Id);
        loadedTask.Title.Value.Should().Be("Write integration tests");
        loadedTask.Description.Should().Be("Cover repository CRUD");
        loadedTask.Status.Should().Be(TaskItemStatus.InProgress);
        loadedTask.DueDate.Value.Should().Be(dueDate);
    }

    [Fact]
    public async Task GetByUserIdAsync_returns_all_tasks_for_a_user()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstCreatedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var secondCreatedAt = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

        var firstTask = TaskItem.Create(firstTaskId, user.Id, "First task", dueDate, firstCreatedAt);
        var secondTask = TaskItem.Create(
            secondTaskId,
            user.Id,
            "Second task",
            dueDate.AddDays(1),
            secondCreatedAt,
            status: TaskItemStatus.Completed);

        _createdTaskIds.AddRange([firstTaskId, secondTaskId]);

        await _fixture.TaskRepository.CreateAsync(firstTask);
        await _fixture.TaskRepository.CreateAsync(secondTask);

        // Act
        var tasks = await _fixture.TaskRepository.GetByUserIdAsync(user.Id);

        // Assert
        tasks.Should().HaveCount(2);
        tasks.Select(task => task.Id).Should().Contain([firstTaskId, secondTaskId]);
        tasks.Select(task => task.Status).Should().Contain(
        [
            TaskItemStatus.Pending,
            TaskItemStatus.Completed
        ]);
    }

    [Fact]
    public async Task GetByUserIdAsync_returns_empty_list_when_user_has_no_tasks()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();

        // Act
        var tasks = await _fixture.TaskRepository.GetByUserIdAsync(user.Id);

        // Assert
        tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_returns_task_when_it_exists()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var task = TaskItem.Create(
            taskId,
            user.Id,
            "Lookup task",
            dueDate,
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        _createdTaskIds.Add(taskId);

        await _fixture.TaskRepository.CreateAsync(task);

        // Act
        var loadedTask = await _fixture.TaskRepository.GetByIdAsync(taskId);

        // Assert
        loadedTask.Should().NotBeNull();
        loadedTask!.Title.Value.Should().Be("Lookup task");
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_task_does_not_exist()
    {
        // Arrange
        var missingTaskId = Guid.NewGuid();

        // Act
        var loadedTask = await _fixture.TaskRepository.GetByIdAsync(missingTaskId);

        // Assert
        loadedTask.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_persists_title_and_status_changes()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var task = TaskItem.Create(
            taskId,
            user.Id,
            "Original title",
            dueDate,
            new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
        _createdTaskIds.Add(taskId);

        await _fixture.TaskRepository.CreateAsync(task);

        task.UpdateTitle("Updated title");
        task.UpdateStatus(TaskItemStatus.Completed);

        // Act
        await _fixture.TaskRepository.UpdateAsync(task);
        var loadedTask = await _fixture.TaskRepository.GetByIdAsync(taskId);

        // Assert
        loadedTask.Should().NotBeNull();
        loadedTask!.Title.Value.Should().Be("Updated title");
        loadedTask.Status.Should().Be(TaskItemStatus.Completed);
    }

    [Fact]
    public async Task DeleteAsync_removes_task_from_database()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var task = TaskItem.Create(
            taskId,
            user.Id,
            "Task to delete",
            dueDate,
            new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc));

        await _fixture.TaskRepository.CreateAsync(task);
        _createdTaskIds.Remove(taskId);

        // Act
        await _fixture.TaskRepository.DeleteAsync(taskId);
        var loadedTask = await _fixture.TaskRepository.GetByIdAsync(taskId);

        // Assert
        loadedTask.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_throws_when_task_does_not_exist()
    {
        // Arrange
        var missingTaskId = Guid.NewGuid();

        // Act
        var act = () => _fixture.TaskRepository.DeleteAsync(missingTaskId);

        // Assert
        await act.Should().ThrowAsync<DataAccessException>();
    }

    [Fact]
    public async Task CreateAsync_stores_sql_injection_style_title_without_corrupting_data()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var taskId = Guid.NewGuid();
        var maliciousTitle = "Title'; DELETE FROM Tasks;--";

        var dueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        var task = TaskItem.Create(
            taskId,
            user.Id,
            maliciousTitle,
            dueDate,
            new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));

        _createdTaskIds.Add(taskId);

        // Act
        await _fixture.TaskRepository.CreateAsync(task);
        var loadedTask = await _fixture.TaskRepository.GetByIdAsync(taskId);
        var userTasks = await _fixture.TaskRepository.GetByUserIdAsync(user.Id);

        // Assert
        loadedTask.Should().NotBeNull();
        loadedTask!.Title.Value.Should().Be(maliciousTitle);
        userTasks.Should().ContainSingle(item => item.Id == taskId);
    }

    [Fact]
    public async Task SearchAsync_returns_paged_results_using_offset_fetch()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var dueDate = DateTime.UtcNow.AddDays(30);
        var taskIds = new List<Guid>();

        for (var index = 0; index < 12; index++)
        {
            var taskId = Guid.NewGuid();
            taskIds.Add(taskId);

            var task = TaskItem.Create(
                taskId,
                user.Id,
                $"Task {index + 1:D2}",
                dueDate.AddDays(index),
                DateTime.UtcNow.AddMinutes(-index));

            _createdTaskIds.Add(taskId);
            await _fixture.TaskRepository.CreateAsync(task);
        }

        var criteria = new TaskSearchCriteria(
            user.Id,
            PageNumber: 2,
            PageSize: 5,
            TitleContains: null,
            Status: null,
            SortBy: TaskSortField.CreatedDate,
            SortDirection: SortDirection.Asc);

        // Act
        var (items, totalRecords) = await _fixture.TaskRepository.SearchAsync(criteria);

        // Assert
        totalRecords.Should().Be(12);
        items.Should().HaveCount(5);
        items.First().Title.Value.Should().Be("Task 07");
        items.Last().Title.Value.Should().Be("Task 03");
    }

    [Fact]
    public async Task SearchAsync_filters_by_title_contains()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var dueDate = DateTime.UtcNow.AddDays(20);

        var reportTaskId = Guid.NewGuid();
        var meetingTaskId = Guid.NewGuid();

        await _fixture.TaskRepository.CreateAsync(TaskItem.Create(
            reportTaskId,
            user.Id,
            "Annual report",
            dueDate,
            DateTime.UtcNow.AddHours(-2)));

        await _fixture.TaskRepository.CreateAsync(TaskItem.Create(
            meetingTaskId,
            user.Id,
            "Team meeting",
            dueDate.AddDays(1),
            DateTime.UtcNow.AddHours(-1)));

        _createdTaskIds.AddRange([reportTaskId, meetingTaskId]);

        var criteria = new TaskSearchCriteria(
            user.Id,
            PageNumber: 1,
            PageSize: 10,
            TitleContains: "report",
            Status: null,
            SortBy: TaskSortField.CreatedDate,
            SortDirection: SortDirection.Desc);

        // Act
        var (items, totalRecords) = await _fixture.TaskRepository.SearchAsync(criteria);

        // Assert
        totalRecords.Should().Be(1);
        items.Should().ContainSingle();
        items[0].Title.Value.Should().Be("Annual report");
    }

    [Fact]
    public async Task SearchAsync_filters_by_status()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var dueDate = DateTime.UtcNow.AddDays(15);

        var pendingTaskId = Guid.NewGuid();
        var completedTaskId = Guid.NewGuid();

        await _fixture.TaskRepository.CreateAsync(TaskItem.Create(
            pendingTaskId,
            user.Id,
            "Pending task",
            dueDate,
            DateTime.UtcNow.AddHours(-2)));

        await _fixture.TaskRepository.CreateAsync(TaskItem.Create(
            completedTaskId,
            user.Id,
            "Completed task",
            dueDate.AddDays(1),
            DateTime.UtcNow.AddHours(-1),
            status: TaskItemStatus.Completed));

        _createdTaskIds.AddRange([pendingTaskId, completedTaskId]);

        var criteria = new TaskSearchCriteria(
            user.Id,
            PageNumber: 1,
            PageSize: 10,
            TitleContains: null,
            Status: TaskItemStatus.Completed,
            SortBy: TaskSortField.CreatedDate,
            SortDirection: SortDirection.Desc);

        // Act
        var (items, totalRecords) = await _fixture.TaskRepository.SearchAsync(criteria);

        // Assert
        totalRecords.Should().Be(1);
        items.Should().ContainSingle();
        items[0].Status.Should().Be(TaskItemStatus.Completed);
    }

    [Fact]
    public async Task SearchAsync_sorts_by_title()
    {
        // Arrange
        var user = await CreatePersistedUserAsync();
        var dueDate = DateTime.UtcNow.AddDays(10);

        var alphaTaskId = Guid.NewGuid();
        var zetaTaskId = Guid.NewGuid();

        await _fixture.TaskRepository.CreateAsync(TaskItem.Create(
            zetaTaskId,
            user.Id,
            "Zeta task",
            dueDate,
            DateTime.UtcNow.AddHours(-1)));

        await _fixture.TaskRepository.CreateAsync(TaskItem.Create(
            alphaTaskId,
            user.Id,
            "Alpha task",
            dueDate.AddDays(1),
            DateTime.UtcNow.AddHours(-2)));

        _createdTaskIds.AddRange([alphaTaskId, zetaTaskId]);

        var criteria = new TaskSearchCriteria(
            user.Id,
            PageNumber: 1,
            PageSize: 10,
            TitleContains: null,
            Status: null,
            SortBy: TaskSortField.Title,
            SortDirection: SortDirection.Asc);

        // Act
        var (items, _) = await _fixture.TaskRepository.SearchAsync(criteria);

        // Assert
        items.Should().HaveCount(2);
        items[0].Title.Value.Should().Be("Alpha task");
        items[1].Title.Value.Should().Be("Zeta task");
    }

    private async Task<User> CreatePersistedUserAsync()
    {
        var userId = Guid.NewGuid();
        var username = $"task_owner_{Guid.NewGuid():N}";
        var user = User.Create(userId, username, "hash-value");

        await _fixture.UserRepository.CreateAsync(user);
        _createdUserIds.Add(userId);

        return user;
    }
}
