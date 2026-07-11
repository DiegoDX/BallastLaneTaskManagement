using Application.DTOs.Agent;

namespace Application.Agent;

public interface IAgentOrchestrator
{
    Task<AgentResponse> RunAsync(
        Guid userId,
        AgentRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentResponse> ContinueAsync(
        Guid userId,
        AgentContinueRequest request,
        CancellationToken cancellationToken = default);
}
