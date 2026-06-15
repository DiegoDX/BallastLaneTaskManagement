using System.Net;
using System.Net.Http.Json;
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
        var (_, tasks) = await response.ReadJsonAsync<List<TaskResponse>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tasks.Should().NotBeNull();
        tasks!.Should().HaveCount(2);
        tasks.Select(task => task.Title).Should().Contain(["First task", "Second task"]);
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
}
