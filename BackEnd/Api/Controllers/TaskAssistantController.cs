using Api.Extensions;
using Application.DTOs.TaskAssistant;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/task-assistant")]
public sealed class TaskAssistantController : ControllerBase
{
    private readonly ITaskAssistantService _taskAssistantService;

    public TaskAssistantController(ITaskAssistantService taskAssistantService)
    {
        _taskAssistantService = taskAssistantService
            ?? throw new ArgumentNullException(nameof(taskAssistantService));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskAssistantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Assist(
        [FromBody] TaskAssistantRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await _taskAssistantService.AssistAsync(userId, request, cancellationToken);

        return Ok(response);
    }
}
