using Application.DTOs.Common;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class TaskServiceTests
{
    private readonly Mock<ITaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<TimeProvider> _timeProviderMock = new();
    private readonly TaskService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _dueDate = DateTime.UtcNow.AddDays(2);
    private readonly DateTime _createdAtUtc = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public TaskServiceTests()
    {
        _timeProviderMock
            .Setup(provider => provider.GetUtcNow())
            .Returns(new DateTimeOffset(_createdAtUtc));

        _sut = new TaskService(
            _taskRepositoryMock.Object,
            _userRepositoryMock.Object,
            _timeProviderMock.Object);
    }

    private TaskItem CreateTaskItem(
        Guid taskId,
        Guid userId,
        string title,
        TaskItemStatus status = TaskItemStatus.Pending,
        string? description = null,
        DateTime? createdAtUtc = null)
    {
        return TaskItem.Create(
            taskId,
            userId,
            title,
            _dueDate,
            createdAtUtc ?? _createdAtUtc,
            description,
            status);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldCreateTaskSuccessfully_WhenDataIsValid()
    {
        // Arrange
        var request = new CreateTaskRequest
        (
            "Write unit tests",
            "Cover application services",
            _dueDate,
            _userId
        );

        var user = User.Restore(_userId, "testuser", "hash");

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        TaskItem? capturedTask = null;

        _taskRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, CancellationToken>((task, _) => capturedTask = task)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _sut.CreateTaskAsync(request);

        // Assert
        response.Title.Should().Be(request.Title);
        response.Description.Should().Be(request.Description);
        response.UserId.Should().Be(_userId);
        response.Status.Should().Be(TaskItemStatus.Pending.ToString());

        capturedTask.Should().NotBeNull();
        capturedTask!.Title.Value.Should().Be(request.Title);

        _taskRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldThrowNotFoundException_WhenUserDoesNotExist()
    {
        // Arrange
        var request = new CreateTaskRequest
        (
            "Write unit tests",
            "Cover application services",
            _dueDate,
            _userId
        );

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = () => _sut.CreateTaskAsync(request);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"User with id '{_userId}' was not found.");

        _taskRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("", "Title is required.")]
    [InlineData("   ", "Title is required.")]
    public async Task CreateTaskAsync_ShouldEnforceDomainRules_WhenTitleIsInvalid(
        string title,
        string expectedMessage)
    {
        // Arrange
        var request = new CreateTaskRequest
        (
            title,
            "Description",
            _dueDate,
            _userId
        );

        var user = User.Restore(_userId, "testuser", "hash");

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var act = () => _sut.CreateTaskAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldThrowValidationException_WhenDueDateIsMissing()
    {
        // Arrange
        var request = new CreateTaskRequest
        (
            "Valid title",
            "Description",
            default,
            _userId
        );

        // Act
        var act = () => _sut.CreateTaskAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Due date is required.");
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldThrowValidationException_WhenRequestIsNull()
    {
        // Act
        var act = () => _sut.CreateTaskAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Create task request is required.");
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldThrowValidationException_WhenUserIdIsEmpty()
    {
        // Arrange
        var request = new CreateTaskRequest
        (
            "Valid title",
            "Description",
            _dueDate,
            Guid.Empty
        );

        // Act
        var act = () => _sut.CreateTaskAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("User id is required.");
    }

    [Fact]
    public async Task CreateTaskAsync_ShouldMapDomainValidationException_WhenDueDateIsInThePast()
    {
        // Arrange
        var request = new CreateTaskRequest
        (
            "Valid title",
            "Description",
            DateTime.Today.AddDays(-1),
            _userId
        );

        var user = User.Restore(_userId, "testuser", "hash");

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var act = () => _sut.CreateTaskAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Due date must be in the future.");

        _taskRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetTaskByIdAsync_ShouldReturnTask_WhenOwnerMatches()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var task = CreateTaskItem(taskId, _userId, "My task", description: "Description");

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var response = await _sut.GetTaskByIdAsync(taskId, _userId);

        // Assert
        response.Id.Should().Be(taskId);
        response.UserId.Should().Be(_userId);
        response.Title.Should().Be("My task");
        response.Description.Should().Be("Description");
        response.Status.Should().Be(TaskItemStatus.Pending.ToString());
    }

    [Fact]
    public async Task GetTaskByIdAsync_ShouldThrowNotFoundException_WhenTaskDoesNotExist()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act
        var act = () => _sut.GetTaskByIdAsync(taskId, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Task with id '{taskId}' was not found.");
    }

    [Fact]
    public async Task GetTaskByIdAsync_ShouldThrowNotFoundException_WhenUserDoesNotOwnTask()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var task = CreateTaskItem(taskId, otherUserId, "Other user task");

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var act = () => _sut.GetTaskByIdAsync(taskId, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Task with id '{taskId}' was not found.");
    }

    [Fact]
    public async Task GetTaskByIdAsync_ShouldThrowValidationException_WhenTaskIdIsEmpty()
    {
        // Act
        var act = () => _sut.GetTaskByIdAsync(Guid.Empty, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Task id is required.");
    }

    [Fact]
    public async Task GetTaskByIdAsync_ShouldThrowValidationException_WhenUserIdIsEmpty()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        // Act
        var act = () => _sut.GetTaskByIdAsync(taskId, Guid.Empty);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("User id is required.");
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldReturnPagedTasksForValidUser()
    {
        // Arrange
        var user = User.Restore(_userId, "testuser", "hash");
        var tasks = new List<TaskItem>
        {
            CreateTaskItem(Guid.NewGuid(), _userId, "Task 1"),
            CreateTaskItem(Guid.NewGuid(), _userId, "Task 2", TaskItemStatus.InProgress)
        };

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _taskRepositoryMock
            .Setup(repository => repository.SearchAsync(It.IsAny<TaskSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, 2));

        var request = new TaskSearchRequest { PageNumber = 1, PageSize = 10 };

        // Act
        var response = await _sut.SearchTasksAsync(_userId, request);

        // Assert
        response.Items.Should().HaveCount(2);
        response.PageNumber.Should().Be(1);
        response.PageSize.Should().Be(10);
        response.TotalRecords.Should().Be(2);
        response.TotalPages.Should().Be(1);
        response.Items.Select(task => task.Title).Should().Contain(["Task 1", "Task 2"]);
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldReturnEmptyPageWhenUserHasNoTasks()
    {
        // Arrange
        var user = User.Restore(_userId, "testuser", "hash");

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _taskRepositoryMock
            .Setup(repository => repository.SearchAsync(It.IsAny<TaskSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<TaskItem>(), 0));

        // Act
        var response = await _sut.SearchTasksAsync(_userId, new TaskSearchRequest());

        // Assert
        response.Items.Should().BeEmpty();
        response.TotalRecords.Should().Be(0);
        response.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldThrowNotFoundException_WhenUserDoesNotExist()
    {
        // Arrange
        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = () => _sut.SearchTasksAsync(_userId, new TaskSearchRequest());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"User with id '{_userId}' was not found.");
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldThrowValidationException_WhenUserIdIsEmpty()
    {
        // Act
        var act = () => _sut.SearchTasksAsync(Guid.Empty, new TaskSearchRequest());

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("User id is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchTasksAsync_ShouldThrowValidationException_WhenPageNumberIsInvalid(int pageNumber)
    {
        // Act
        var act = () => _sut.SearchTasksAsync(
            _userId,
            new TaskSearchRequest { PageNumber = pageNumber, PageSize = 10 });

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PageNumber must be greater than 0.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task SearchTasksAsync_ShouldThrowValidationException_WhenPageSizeIsInvalid(int pageSize)
    {
        // Act
        var act = () => _sut.SearchTasksAsync(
            _userId,
            new TaskSearchRequest { PageNumber = 1, PageSize = pageSize });

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PageSize must be between 1 and 100.");
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldThrowValidationException_WhenStatusIsInvalid()
    {
        // Act
        var act = () => _sut.SearchTasksAsync(
            _userId,
            new TaskSearchRequest { Status = "NotAStatus" });

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Status must be one of: Pending, InProgress, or Completed.");
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldPassStatusFilterToRepository()
    {
        // Arrange
        var user = User.Restore(_userId, "testuser", "hash");
        TaskSearchCriteria? capturedCriteria = null;

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _taskRepositoryMock
            .Setup(repository => repository.SearchAsync(It.IsAny<TaskSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<TaskSearchCriteria, CancellationToken>((criteria, _) => capturedCriteria = criteria)
            .ReturnsAsync((Array.Empty<TaskItem>(), 0));

        // Act
        await _sut.SearchTasksAsync(
            _userId,
            new TaskSearchRequest { Status = TaskItemStatus.Completed.ToString() });

        // Assert
        capturedCriteria.Should().NotBeNull();
        capturedCriteria!.Status.Should().Be(TaskItemStatus.Completed);
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldPassTitleFilterToRepository()
    {
        // Arrange
        var user = User.Restore(_userId, "testuser", "hash");
        TaskSearchCriteria? capturedCriteria = null;

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _taskRepositoryMock
            .Setup(repository => repository.SearchAsync(It.IsAny<TaskSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<TaskSearchCriteria, CancellationToken>((criteria, _) => capturedCriteria = criteria)
            .ReturnsAsync((Array.Empty<TaskItem>(), 0));

        // Act
        await _sut.SearchTasksAsync(
            _userId,
            new TaskSearchRequest { Title = "  report  " });

        // Assert
        capturedCriteria.Should().NotBeNull();
        capturedCriteria!.TitleContains.Should().Be("report");
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldUseCreatedDateDescendingByDefault()
    {
        // Arrange
        var user = User.Restore(_userId, "testuser", "hash");
        TaskSearchCriteria? capturedCriteria = null;

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _taskRepositoryMock
            .Setup(repository => repository.SearchAsync(It.IsAny<TaskSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<TaskSearchCriteria, CancellationToken>((criteria, _) => capturedCriteria = criteria)
            .ReturnsAsync((Array.Empty<TaskItem>(), 0));

        // Act
        await _sut.SearchTasksAsync(_userId, new TaskSearchRequest());

        // Assert
        capturedCriteria.Should().NotBeNull();
        capturedCriteria!.SortBy.Should().Be(TaskSortField.CreatedDate);
        capturedCriteria.SortDirection.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldPassSortCriteriaToRepository()
    {
        // Arrange
        var user = User.Restore(_userId, "testuser", "hash");
        TaskSearchCriteria? capturedCriteria = null;

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _taskRepositoryMock
            .Setup(repository => repository.SearchAsync(It.IsAny<TaskSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<TaskSearchCriteria, CancellationToken>((criteria, _) => capturedCriteria = criteria)
            .ReturnsAsync((Array.Empty<TaskItem>(), 0));

        // Act
        await _sut.SearchTasksAsync(
            _userId,
            new TaskSearchRequest
            {
                SortBy = TaskSortField.Title.ToString(),
                SortDirection = SortDirection.Desc.ToString()
            });

        // Assert
        capturedCriteria.Should().NotBeNull();
        capturedCriteria!.SortBy.Should().Be(TaskSortField.Title);
        capturedCriteria.SortDirection.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldThrowValidationException_WhenFilteringForAnotherUser()
    {
        // Act
        var act = () => _sut.SearchTasksAsync(
            _userId,
            new TaskSearchRequest { CreatedByUserId = Guid.NewGuid() });

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Cannot filter tasks for another user.");
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldThrowValidationException_WhenSortByIsInvalid()
    {
        // Act
        var act = () => _sut.SearchTasksAsync(
            _userId,
            new TaskSearchRequest { SortBy = "DueDate" });

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("SortBy must be one of: CreatedDate, Title, or Status.");
    }

    [Fact]
    public async Task SearchTasksAsync_ShouldThrowValidationException_WhenSortDirectionIsInvalid()
    {
        // Act
        var act = () => _sut.SearchTasksAsync(
            _userId,
            new TaskSearchRequest { SortDirection = "Up" });

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("SortDirection must be Asc or Desc.");
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldUpdateTaskSuccessfully()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = CreateTaskItem(taskId, _userId, "Original title");

        var request = new UpdateTaskRequest(taskId, "Updated title", "Updated description", TaskItemStatus.InProgress.ToString());
        
        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        _taskRepositoryMock
            .Setup(repository => repository.UpdateAsync(existingTask, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateTaskAsync(request, _userId);

        // Assert
        existingTask.Title.Value.Should().Be("Updated title");
        existingTask.Status.Should().Be(TaskItemStatus.InProgress);

        _taskRepositoryMock.Verify(
            repository => repository.UpdateAsync(existingTask, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldThrowNotFoundException_WhenTaskDoesNotExist()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var request = new UpdateTaskRequest(taskId, "Updated title", "desc", TaskItemStatus.InProgress.ToString());

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act
        var act = () => _sut.UpdateTaskAsync(request, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Task with id '{taskId}' was not found.");
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldValidateStatusTransitions()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var completedTask = CreateTaskItem(
            taskId,
            _userId,
            "Completed task",
            TaskItemStatus.Completed);

        var request = new UpdateTaskRequest(taskId, "Updated title", "Updated description", TaskItemStatus.Pending.ToString());

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(completedTask);

        // Act
        var act = () => _sut.UpdateTaskAsync(request, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("A completed task cannot change status.");

        _taskRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldThrowNotFoundException_WhenUserDoesNotOwnTask()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var task = CreateTaskItem(taskId, otherUserId, "Other user task");

        var request = new UpdateTaskRequest(taskId, "Updated title", "Updated description", TaskItemStatus.InProgress.ToString());

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var act = () => _sut.UpdateTaskAsync(request, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Task with id '{taskId}' was not found.");

        _taskRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldThrowValidationException_WhenRequestIsNull()
    {
        // Act
        var act = () => _sut.UpdateTaskAsync(null!, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Update task request is required.");
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldThrowValidationException_WhenTaskIdIsEmpty()
    {
        // Arrange
        var request = new UpdateTaskRequest(Guid.Empty, "Updated title", "Updated description", TaskItemStatus.InProgress.ToString());

        // Act
        var act = () => _sut.UpdateTaskAsync(request, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Task id is required.");
    }

    [Theory]
    [InlineData("", "Title is required.")]
    [InlineData("   ", "Title is required.")]
    public async Task UpdateTaskAsync_ShouldThrowValidationException_WhenTitleIsMissing(
        string title,
        string expectedMessage)
    {
        // Arrange
        var request = new UpdateTaskRequest(Guid.NewGuid(), title, "Description", TaskItemStatus.InProgress.ToString());

        // Act
        var act = () => _sut.UpdateTaskAsync(request, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(expectedMessage);
    }

    [Theory]
    [InlineData("", "Status is required.")]
    [InlineData("   ", "Status is required.")]
    public async Task UpdateTaskAsync_ShouldThrowValidationException_WhenStatusIsMissing(
        string status,
        string expectedMessage)
    {
        // Arrange
        var request = new UpdateTaskRequest(Guid.NewGuid(), "Updated title", "Updated description", status);

        // Act
        var act = () => _sut.UpdateTaskAsync(request, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldThrowValidationException_WhenUserIdIsEmpty()
    {
        // Arrange
        var request = new UpdateTaskRequest(Guid.NewGuid(), "Updated title", "desc", TaskItemStatus.InProgress.ToString());

        // Act
        var act = () => _sut.UpdateTaskAsync(request, Guid.Empty);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("User id is required.");
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldThrowValidationException_WhenStatusIsInvalid()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = CreateTaskItem(taskId, _userId, "Original title");

        var request = new UpdateTaskRequest(taskId, "Updated title", "Updated description", "NotAStatus");

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var act = () => _sut.UpdateTaskAsync(request, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Status must be one of: Pending, InProgress, or Completed.");

        _taskRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldAllowSameStatusWithoutTransitionError()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = CreateTaskItem(taskId, _userId, "Original title");

        var request = new UpdateTaskRequest(taskId, "Updated title", "Updated description", TaskItemStatus.Pending.ToString());

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        _taskRepositoryMock
            .Setup(repository => repository.UpdateAsync(existingTask, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateTaskAsync(request, _userId);

        // Assert
        existingTask.Status.Should().Be(TaskItemStatus.Pending);
        existingTask.Title.Value.Should().Be("Updated title");

        _taskRepositoryMock.Verify(
            repository => repository.UpdateAsync(existingTask, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldAllowTransition_FromInProgressToPending()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = CreateTaskItem(
            taskId,
            _userId,
            "In progress task",
            TaskItemStatus.InProgress);

        var request = new UpdateTaskRequest(taskId, "In progress task", "In progress task description", TaskItemStatus.Pending.ToString());

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        _taskRepositoryMock
            .Setup(repository => repository.UpdateAsync(existingTask, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateTaskAsync(request, _userId);

        // Assert
        existingTask.Status.Should().Be(TaskItemStatus.Pending);
    }

    [Fact]
    public async Task UpdateTaskAsync_ShouldMapDomainValidationException_WhenTitleIsInvalid()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var existingTask = CreateTaskItem(taskId, _userId, "Original title");
        var tooLongTitle = new string('a', 257);

        var request = new UpdateTaskRequest(taskId, tooLongTitle, "Description", TaskItemStatus.InProgress.ToString());

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        // Act
        var act = () => _sut.UpdateTaskAsync(request, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Title must be at most {MaxLength} characters long.");

        _taskRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteTaskAsync_ShouldDeleteTaskSuccessfully()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var task = CreateTaskItem(taskId, _userId, "Task to delete");

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _taskRepositoryMock
            .Setup(repository => repository.DeleteAsync(taskId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteTaskAsync(taskId, _userId);

        // Assert
        _taskRepositoryMock.Verify(
            repository => repository.DeleteAsync(taskId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteTaskAsync_ShouldThrowNotFoundException_WhenTaskDoesNotExist()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act
        var act = () => _sut.DeleteTaskAsync(taskId, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Task with id '{taskId}' was not found.");

        _taskRepositoryMock.Verify(
            repository => repository.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteTaskAsync_ShouldThrowNotFoundException_WhenUserDoesNotOwnTask()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var task = CreateTaskItem(taskId, otherUserId, "Other user task");

        _taskRepositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var act = () => _sut.DeleteTaskAsync(taskId, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Task with id '{taskId}' was not found.");

        _taskRepositoryMock.Verify(
            repository => repository.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteTaskAsync_ShouldThrowValidationException_WhenTaskIdIsEmpty()
    {
        // Act
        var act = () => _sut.DeleteTaskAsync(Guid.Empty, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Task id is required.");
    }

    [Fact]
    public async Task DeleteTaskAsync_ShouldThrowValidationException_WhenUserIdIsEmpty()
    {
        // Arrange
        var taskId = Guid.NewGuid();

        // Act
        var act = () => _sut.DeleteTaskAsync(taskId, Guid.Empty);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("User id is required.");
    }
}
