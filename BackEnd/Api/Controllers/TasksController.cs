using Api.Extensions;
using Application.DTOs.Tasks;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/tasks")]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTasks(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var tasks = await _taskService
            .GetTasksByUserAsync(userId, cancellationToken);

        return Ok(tasks);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTask(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var task = await _taskService
            .GetTaskByIdAsync(id, userId, cancellationToken);

        return Ok(task);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateTask(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var createRequest = new CreateTaskRequest(request.Title, request.Description, request.DueDate, userId);
        
        var task = await _taskService
            .CreateTaskAsync(createRequest, cancellationToken);

        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTask(
        Guid id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var updateRequest = new UpdateTaskRequest(id, request.Title, request.Description, request.Status);

        await _taskService
            .UpdateTaskAsync(updateRequest, userId, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await _taskService
            .DeleteTaskAsync(id, userId, cancellationToken);

        return NoContent();
    }
}
