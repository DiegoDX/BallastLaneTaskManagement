using System.Text.Json;
using Application.Agent.Specialists;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;

namespace Application.Agent.Phases;

public sealed class SummaryPhaseHandler : IAgentPhaseHandler
{
    private readonly ISummaryAgent _summaryAgent;

    public SummaryPhaseHandler(ISummaryAgent summaryAgent)
    {
        _summaryAgent = summaryAgent ?? throw new ArgumentNullException(nameof(summaryAgent));
    }

    public string PhaseName => AgentPhaseNames.Summary;

    public async Task<AgentPhaseOutcome> HandleAsync(
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context.Plan);

        if (context.Status == AgentRunStatus.Rejected)
        {
            return new AgentPhaseOutcome(AgentPhaseStatus.Skipped);
        }

        var result = await _summaryAgent.SummarizeAsync(
            new SummaryAgentRequest(context.Plan, context.Review, context.Actions, context.Status),
            cancellationToken);

        context.Summary = result.Summary;
        context.Status = AgentRunStatus.Completed;
        context.Model ??= result.Model;

        return new AgentPhaseOutcome(
            AgentPhaseStatus.Completed,
            result.OutputJson ?? JsonSerializer.Serialize(new { summary = result.Summary }));
    }
}
