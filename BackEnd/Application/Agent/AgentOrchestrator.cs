using System.Diagnostics;
using Application.DTOs.Agent;
using Application.Exceptions;

namespace Application.Agent;

public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IReadOnlyList<IAgentPhaseHandler> _phaseHandlers;
    private readonly IAgentRunStore _runStore;

    public AgentOrchestrator(
        IEnumerable<IAgentPhaseHandler> phaseHandlers,
        IAgentRunStore runStore)
    {
        _phaseHandlers = phaseHandlers?.ToList()
            ?? throw new ArgumentNullException(nameof(phaseHandlers));

        if (_phaseHandlers.Count == 0)
        {
            throw new ArgumentException("At least one agent phase handler is required.", nameof(phaseHandlers));
        }

        _runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
    }

    public async Task<AgentResponse> RunAsync(
        Guid userId,
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = new AgentRunContext
        {
            RunId = Guid.NewGuid(),
            UserId = userId,
            Messages = request.Messages,
            Status = AgentRunStatus.Completed
        };

        return await ExecuteWorkflowAsync(context, cancellationToken);
    }

    public async Task<AgentResponse> ContinueAsync(
        Guid userId,
        AgentContinueRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = await _runStore.GetAsync(request.RunId, userId, cancellationToken);

        if (context is null)
        {
            throw new NotFoundException("Agent run was not found or has expired.");
        }

        context.Approved = request.Approved;
        context.IsContinuation = true;
        context.Phases.Clear();
        context.Status = AgentRunStatus.Completed;

        var response = await ExecuteWorkflowAsync(context, cancellationToken);

        if (context.Status != AgentRunStatus.AwaitingApproval)
        {
            await _runStore.RemoveAsync(context.RunId, userId, cancellationToken);
        }

        return response;
    }

    private async Task<AgentResponse> ExecuteWorkflowAsync(
        AgentRunContext context,
        CancellationToken cancellationToken)
    {
        foreach (var handler in _phaseHandlers)
        {
            var stopwatch = Stopwatch.StartNew();
            var outcome = await handler.HandleAsync(context, cancellationToken);
            stopwatch.Stop();

            context.Phases.Add(new AgentPhaseResult(
                handler.PhaseName,
                outcome.Status,
                outcome.OutputJson,
                outcome.Status == AgentPhaseStatus.Skipped ? 0 : (int)stopwatch.ElapsedMilliseconds));

            if (outcome.ShouldStop)
            {
                if (context.Status == AgentRunStatus.AwaitingApproval)
                {
                    await _runStore.SaveAsync(context, cancellationToken);
                    context.Summary ??= "Review the plan and approve or reject to continue.";
                }

                return BuildResponse(context);
            }
        }

        if (context.Status != AgentRunStatus.Rejected)
        {
            context.Status = AgentRunStatus.Completed;
        }

        context.Summary ??= "The agent completed without a summary.";

        return BuildResponse(context);
    }

    private static AgentResponse BuildResponse(AgentRunContext context) =>
        new(
            context.Summary ?? string.Empty,
            context.Phases,
            context.Actions,
            context.ExecutionReport,
            context.Plan,
            context.Status,
            context.Status == AgentRunStatus.AwaitingApproval ? context.RunId : null,
            context.Model);
}
