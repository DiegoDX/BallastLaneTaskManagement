using Application.DTOs.Agent;

namespace Application.Interfaces.Services;

public interface IAgentService
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
