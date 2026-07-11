using Api.Extensions;
using Application.DTOs.DocAssistant;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/doc-assistant")]
public sealed class DocAssistantController : ControllerBase
{
    private readonly IDocAssistantService _docAssistantService;

    public DocAssistantController(IDocAssistantService docAssistantService)
    {
        _docAssistantService = docAssistantService
            ?? throw new ArgumentNullException(nameof(docAssistantService));
    }

    [HttpPost]
    [ProducesResponseType(typeof(DocAssistantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ask(
        [FromBody] DocAssistantRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await _docAssistantService.AskAsync(userId, request, cancellationToken);

        return Ok(response);
    }

    [HttpPost("reindex")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reindex(CancellationToken cancellationToken)
    {
        await _docAssistantService.ReindexAsync(cancellationToken);

        return Ok();
    }
}
