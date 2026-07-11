using Application.DTOs.Agent;

namespace Application.Agent;

public interface IAgentPhaseHandler
{
    string PhaseName { get; }

    Task<AgentPhaseOutcome> HandleAsync(
        AgentRunContext context,
        CancellationToken cancellationToken = default);
}
