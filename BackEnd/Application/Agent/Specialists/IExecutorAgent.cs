using Application.DTOs.Agent.Specialists;

namespace Application.Agent.Specialists;

public interface IExecutorAgent
{
    Task<ExecutorAgentResult> ExecuteAsync(
        ExecutorAgentRequest request,
        CancellationToken cancellationToken = default);
}
