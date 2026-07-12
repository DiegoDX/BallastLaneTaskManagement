using System.Text.Json;
using Application.Agent.Specialists;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;

namespace Application.Agent.Phases;

public sealed class ReviewPhaseHandler : IAgentPhaseHandler
{
    private readonly IReviewerAgent _reviewerAgent;

    public ReviewPhaseHandler(IReviewerAgent reviewerAgent)
    {
        _reviewerAgent = reviewerAgent ?? throw new ArgumentNullException(nameof(reviewerAgent));
    }

    public string PhaseName => AgentPhaseNames.Review;

    public async Task<AgentPhaseOutcome> HandleAsync(
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context.Plan);

        var result = await _reviewerAgent.ReviewAsync(
            new ReviewerAgentRequest(context.Plan, context.ExecutionReport, context.Actions),
            cancellationToken);

        context.Review = result.Review;
        context.Model ??= result.Model;

        return new AgentPhaseOutcome(
            AgentPhaseStatus.Completed,
            JsonSerializer.Serialize(result.Review));
    }
}
