using Application.DTOs.Agent.Specialists;

namespace Application.Agent.Specialists;

public interface IPlannerAgent
{
    Task<PlannerAgentResult> PlanAsync(
        PlannerAgentRequest request,
        CancellationToken cancellationToken = default);
}
