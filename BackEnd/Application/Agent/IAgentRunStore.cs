namespace Application.Agent;

public interface IAgentRunStore
{
    Task SaveAsync(AgentRunContext context, CancellationToken cancellationToken = default);

    Task<AgentRunContext?> GetAsync(
        Guid runId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid runId, Guid userId, CancellationToken cancellationToken = default);
}
