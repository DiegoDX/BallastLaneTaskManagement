using Api.Extensions;
using Application.DTOs.Tasks;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/tasks")]
public sealed class TaskSuggestionsController : ControllerBase
{
    private readonly ITaskSuggestionService _taskSuggestionService;

    public TaskSuggestionsController(ITaskSuggestionService taskSuggestionService)
    {
        _taskSuggestionService = taskSuggestionService
            ?? throw new ArgumentNullException(nameof(taskSuggestionService));
    }

    [HttpPost("suggestions")]
    [ProducesResponseType(typeof(TaskSuggestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Suggest(
        [FromBody] TaskSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var suggestion = await _taskSuggestionService
            .SuggestAsync(userId, request, cancellationToken);

        return Ok(suggestion);
    }

    [HttpPost("suggestions/create")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateFromSuggestions(
        [FromBody] TaskSuggestionCreateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var createdTasks = await _taskSuggestionService
            .CreateFromSuggestionsAsync(userId, request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, createdTasks);
    }
}
