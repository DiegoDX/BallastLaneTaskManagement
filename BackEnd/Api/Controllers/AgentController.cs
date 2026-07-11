using Api.Extensions;
using Application.DTOs.Agent;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/agent")]
public sealed class AgentController : ControllerBase
{
    private readonly IAgentService _agentService;

    public AgentController(IAgentService agentService)
    {
        _agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Run(
        [FromBody] AgentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var response = await _agentService.RunAsync(userId, request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("continue")]
    [ProducesResponseType(typeof(AgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Continue(
        [FromBody] AgentContinueRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var response = await _agentService.ContinueAsync(userId, request, cancellationToken);
        return Ok(response);
    }
}
