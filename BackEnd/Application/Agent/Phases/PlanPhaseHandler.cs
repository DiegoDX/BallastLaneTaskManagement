using System.Text.Json;
using Application.Agent.Specialists;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;

namespace Application.Agent.Phases;

public sealed class PlanPhaseHandler : IAgentPhaseHandler
{
    private readonly IPlannerAgent _plannerAgent;

    public PlanPhaseHandler(IPlannerAgent plannerAgent)
    {
        _plannerAgent = plannerAgent ?? throw new ArgumentNullException(nameof(plannerAgent));
    }

    public string PhaseName => AgentPhaseNames.Plan;

    public async Task<AgentPhaseOutcome> HandleAsync(
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Plan is not null && context.IsContinuation)
        {
            return new AgentPhaseOutcome(AgentPhaseStatus.Skipped);
        }

        var result = await _plannerAgent.PlanAsync(
            new PlannerAgentRequest(context.UserId, context.Messages),
            cancellationToken);

        context.Plan = result.Plan;
        context.Model ??= result.Model;

        return new AgentPhaseOutcome(
            AgentPhaseStatus.Completed,
            JsonSerializer.Serialize(result.Plan));
    }
}
