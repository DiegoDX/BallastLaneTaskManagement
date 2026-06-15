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

        var task = TaskItem.Create(
            taskId,
            user.Id,
            "Write integration tests",
            dueDate,
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

        var firstTask = TaskItem.Create(firstTaskId, user.Id, "First task", dueDate);
        var secondTask = TaskItem.Create(
            secondTaskId,
            user.Id,
            "Second task",
            dueDate.AddDays(1),
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
        var task = TaskItem.Create(taskId, user.Id, "Lookup task", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
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
        var task = TaskItem.Create(taskId, user.Id, "Original title", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
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
        var task = TaskItem.Create(taskId, user.Id, "Task to delete", new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

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

        var task = TaskItem.Create(
            taskId,
            user.Id,
            maliciousTitle,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

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
