using System.Text.Json;
using Application.Agent.Specialists;
using Application.DTOs.Agent;
using Application.DTOs.Agent.Specialists;

namespace Application.Agent.Phases;

public sealed class ExecutePhaseHandler : IAgentPhaseHandler
{
    private readonly IExecutorAgent _executorAgent;

    public ExecutePhaseHandler(IExecutorAgent executorAgent)
    {
        _executorAgent = executorAgent ?? throw new ArgumentNullException(nameof(executorAgent));
    }

    public string PhaseName => AgentPhaseNames.Execute;

    public async Task<AgentPhaseOutcome> HandleAsync(
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context.Plan);

        var result = await _executorAgent.ExecuteAsync(
            new ExecutorAgentRequest(
                context.UserId,
                context.Messages,
                context.Plan,
                context.ExecuteMessages,
                context.ReExecutionHint),
            cancellationToken);

        context.ExecutionReport = result.ExecutionReport;
        context.Model ??= result.Model;
        context.ReExecutionHint = null;

        context.ExecuteMessages.Clear();
        context.ExecuteMessages.AddRange(result.ExecuteMessages);

        foreach (var action in result.Actions)
        {
            context.Actions.Add(action);
        }

        var outputJson = JsonSerializer.Serialize(new
        {
            result.ExecutionReport.Iterations,
            assistantMessage = result.AssistantMessage
        });

        return new AgentPhaseOutcome(AgentPhaseStatus.Completed, outputJson);
    }
}
