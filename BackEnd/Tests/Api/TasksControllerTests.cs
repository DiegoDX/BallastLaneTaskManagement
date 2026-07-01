using System.Net;
using System.Net.Http.Json;
using Application.DTOs.Common;
using Application.DTOs.Tasks;
using FluentAssertions;

namespace Tests.Api;

[Collection("ApiIntegration")]
[Trait("Category", "ApiIntegration")]
public sealed class TasksControllerTests
{
    private readonly ApiIntegrationFixture _factory;

    public TasksControllerTests(ApiIntegrationFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_tasks_returns_ok_with_tasks_for_authenticated_user()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var dueDate = DateTime.UtcNow.AddDays(3);

        await CreateTaskAsync(authenticatedClient, "First task", dueDate);
        await CreateTaskAsync(authenticatedClient, "Second task", dueDate.AddDays(1));

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(HttpMethod.Get, ApiRoutes.Tasks);
        var (_, pagedTasks) = await response.ReadJsonAsync<PagedResult<TaskListItemResponse>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        pagedTasks.Should().NotBeNull();
        pagedTasks!.Items.Should().HaveCount(2);
        pagedTasks.PageNumber.Should().Be(1);
        pagedTasks.PageSize.Should().Be(10);
        pagedTasks.TotalRecords.Should().Be(2);
        pagedTasks.TotalPages.Should().Be(1);
        pagedTasks.Items.Select(task => task.Title).Should().Contain(["First task", "Second task"]);
    }

    [Fact]
    public async Task Get_tasks_returns_paged_metadata_for_query_parameters()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var dueDate = DateTime.UtcNow.AddDays(3);

        for (var index = 0; index < 12; index++)
        {
            await CreateTaskAsync(authenticatedClient, $"Paged task {index + 1:D2}", dueDate.AddDays(index));
        }

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Tasks}?pageNumber=2&pageSize=5");

        var (_, pagedTasks) = await response.ReadJsonAsync<PagedResult<TaskListItemResponse>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        pagedTasks.Should().NotBeNull();
        pagedTasks!.PageNumber.Should().Be(2);
        pagedTasks.PageSize.Should().Be(5);
        pagedTasks.TotalRecords.Should().Be(12);
        pagedTasks.TotalPages.Should().Be(3);
        pagedTasks.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Get_tasks_filters_by_status()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var dueDate = DateTime.UtcNow.AddDays(3);

        var pendingTask = await CreateTaskAsync(authenticatedClient, "Pending task", dueDate);
        var completedTask = await CreateTaskAsync(authenticatedClient, "Completed task", dueDate.AddDays(1));

        await UpdateTaskStatusAsync(authenticatedClient, completedTask.Id, "Completed");

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Tasks}?status=Completed");

        var (_, pagedTasks) = await response.ReadJsonAsync<PagedResult<TaskListItemResponse>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        pagedTasks!.TotalRecords.Should().Be(1);
        pagedTasks.Items.Should().ContainSingle(task => task.Id == completedTask.Id);
        pagedTasks.Items.Should().NotContain(task => task.Id == pendingTask.Id);
    }

    [Fact]
    public async Task Get_tasks_filters_by_title_contains()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var dueDate = DateTime.UtcNow.AddDays(3);

        await CreateTaskAsync(authenticatedClient, "Annual report", dueDate);
        await CreateTaskAsync(authenticatedClient, "Team meeting", dueDate.AddDays(1));

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Tasks}?title=report");

        var (_, pagedTasks) = await response.ReadJsonAsync<PagedResult<TaskListItemResponse>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        pagedTasks!.TotalRecords.Should().Be(1);
        pagedTasks.Items.Should().ContainSingle(task => task.Title == "Annual report");
    }

    [Theory]
    [InlineData("pageNumber=0&pageSize=10", "PageNumber must be greater than 0.")]
    [InlineData("pageNumber=1&pageSize=0", "PageSize must be between 1 and 100.")]
    [InlineData("pageNumber=1&pageSize=101", "PageSize must be between 1 and 100.")]
    [InlineData("status=NotAStatus", "Status must be one of: Pending, InProgress, or Completed.")]
    public async Task Get_tasks_returns_bad_request_for_invalid_query_parameters(
        string query,
        string expectedMessage)
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Get,
            $"{ApiRoutes.Tasks}?{query}");

        var (_, error) = await response.ReadErrorAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public async Task Get_tasks_returns_unauthorized_when_token_is_missing()
    {
        // Act
        var response = await _factory.HttpClient.GetAsync(ApiRoutes.Tasks);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_task_by_id_returns_ok_when_task_exists()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var createdTask = await CreateTaskAsync(
            authenticatedClient,
            "Existing task",
            DateTime.UtcNow.AddDays(5));

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Get,
            ApiRoutes.TaskById(createdTask.Id));

        var (_, task) = await response.ReadJsonAsync<TaskResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        task.Should().NotBeNull();
        task!.Id.Should().Be(createdTask.Id);
        task.Title.Should().Be("Existing task");
    }

    [Fact]
    public async Task Get_task_by_id_returns_not_found_when_task_does_not_exist()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var missingTaskId = Guid.NewGuid();

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Get,
            ApiRoutes.TaskById(missingTaskId));

        var (_, error) = await response.ReadErrorAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        error!.Message.Should().Contain(missingTaskId.ToString());
    }

    [Fact]
    public async Task Get_task_by_id_returns_unauthorized_when_token_is_missing()
    {
        // Act
        var response = await _factory.HttpClient.GetAsync(ApiRoutes.TaskById(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_task_returns_created_when_request_is_valid()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new CreateTaskRequest
        (
            "New API task",
            "Created through integration test",
            DateTime.UtcNow.AddDays(2),
            authenticatedClient.UserId
        );

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Post,
            ApiRoutes.Tasks,
            JsonContent.Create(request));

        var (_, task) = await response.ReadJsonAsync<TaskResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        task.Should().NotBeNull();
        task!.Title.Should().Be("New API task");
        task.UserId.Should().Be(authenticatedClient.UserId);
    }

    [Fact]
    public async Task Create_task_returns_bad_request_when_required_fields_are_missing()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new CreateTaskRequest
        (
            "",
            "",
            DateTime.UtcNow.AddDays(2),
            authenticatedClient.UserId
        );
        
        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Post,
            ApiRoutes.Tasks,
            JsonContent.Create(request));

        var (_, error) = await response.ReadErrorAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Message.Should().Be("Title is required.");
    }

    [Fact]
    public async Task Create_task_returns_unauthorized_when_token_is_missing()
    {
        // Arrange
        var request = new CreateTaskRequest
        (
            "Unauthorized create",
            "This should fail",
            DateTime.UtcNow.AddDays(2),
            Guid.Empty
        );

        // Act
        var response = await _factory.HttpClient.PostAsJsonAsync(ApiRoutes.Tasks, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_task_returns_no_content_when_update_is_successful()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var createdTask = await CreateTaskAsync(
            authenticatedClient,
            "Task to update",
            DateTime.UtcNow.AddDays(4));

        var updateRequest = new UpdateTaskRequest(createdTask.Id, "Updated task title", "Updated task description", "InProgress");

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Put,
            ApiRoutes.TaskById(createdTask.Id),
            JsonContent.Create(updateRequest));

        var getResponse = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Get,
            ApiRoutes.TaskById(createdTask.Id));

        var (_, updatedTask) = await getResponse.ReadJsonAsync<TaskResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        updatedTask!.Title.Should().Be("Updated task title");
        updatedTask.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task Update_task_returns_not_found_when_task_does_not_exist()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var missingTaskId = Guid.NewGuid();
        var updateRequest = new UpdateTaskRequest(missingTaskId, "Non-existent task", "desc", "Completed");

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Put,
            ApiRoutes.TaskById(missingTaskId),
            JsonContent.Create(updateRequest));

        var (_, error) = await response.ReadErrorAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        error!.Message.Should().Contain(missingTaskId.ToString());
    }

    [Fact]
    public async Task Update_task_returns_unauthorized_when_token_is_missing()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var updateRequest = new UpdateTaskRequest(taskId, "Unauthorized update", "desc", "Pending");
    
        // Act
        var response = await _factory.HttpClient.PutAsJsonAsync(
            ApiRoutes.TaskById(taskId),
            updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_task_returns_no_content_when_deletion_is_successful()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var createdTask = await CreateTaskAsync(
            authenticatedClient,
            "Task to delete",
            DateTime.UtcNow.AddDays(1));

        // Act
        var deleteResponse = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Delete,
            ApiRoutes.TaskById(createdTask.Id));

        var getResponse = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Get,
            ApiRoutes.TaskById(createdTask.Id));

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_task_returns_not_found_when_task_does_not_exist()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var missingTaskId = Guid.NewGuid();

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Delete,
            ApiRoutes.TaskById(missingTaskId));

        var (_, error) = await response.ReadErrorAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        error!.Message.Should().Contain(missingTaskId.ToString());
    }

    [Fact]
    public async Task Delete_task_returns_unauthorized_when_token_is_missing()
    {
        // Act
        var response = await _factory.HttpClient.DeleteAsync(ApiRoutes.TaskById(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<AuthenticatedApiClient> CreateRegisteredUserAsync()
    {
        var username = $"tasks_user_{Guid.NewGuid():N}";
        var client = await ApiAuthHelper.RegisterAndLoginAsync(_factory.HttpClient, username, "password123");
        _factory.TrackUser(client.UserId);
        return client;
    }

    private static async Task<TaskResponse> CreateTaskAsync(
        AuthenticatedApiClient authenticatedClient,
        string title,
        DateTime dueDate)
    {
        var request = new CreateTaskRequest
        (
            title,
            "Task created for testing",
            dueDate,
            authenticatedClient.UserId
        );

        var response = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Post,
            ApiRoutes.Tasks,
            JsonContent.Create(request));

        var (_, task) = await response.ReadJsonAsync<TaskResponse>();
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        task.Should().NotBeNull();

        return task!;
    }

    private static async Task UpdateTaskStatusAsync(
        AuthenticatedApiClient authenticatedClient,
        Guid taskId,
        string status)
    {
        var getResponse = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Get,
            ApiRoutes.TaskById(taskId));

        var (_, existingTask) = await getResponse.ReadJsonAsync<TaskResponse>();
        existingTask.Should().NotBeNull();

        var updateRequest = new UpdateTaskRequest(
            taskId,
            existingTask!.Title,
            existingTask.Description,
            status);

        var updateResponse = await authenticatedClient.SendAuthorizedAsync(
            HttpMethod.Put,
            ApiRoutes.TaskById(taskId),
            JsonContent.Create(updateRequest));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
