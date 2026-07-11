using Application.DTOs.Agent;
using Application.Llm.TaskAssistant;
using Microsoft.Extensions.Options;

namespace Application.Agent.Phases;

public sealed class ApprovalPhaseHandler : IAgentPhaseHandler
{
    private readonly AgentOptions _options;

    public ApprovalPhaseHandler(IOptions<AgentOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string PhaseName => AgentPhaseNames.Approval;

    public Task<AgentPhaseOutcome> HandleAsync(
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context.Plan);

        if (!RequiresApproval(context.Plan))
        {
            return Task.FromResult(new AgentPhaseOutcome(AgentPhaseStatus.Skipped));
        }

        if (context.IsContinuation)
        {
            if (!context.Approved)
            {
                context.Status = AgentRunStatus.Rejected;
                context.Summary = "The plan was rejected. No changes were made.";
                return Task.FromResult(new AgentPhaseOutcome(
                    AgentPhaseStatus.Completed,
                    ShouldStop: true));
            }

            return Task.FromResult(new AgentPhaseOutcome(AgentPhaseStatus.Completed));
        }

        context.Status = AgentRunStatus.AwaitingApproval;

        return Task.FromResult(new AgentPhaseOutcome(
            AgentPhaseStatus.Waiting,
            ShouldStop: true));
    }

    private bool RequiresApproval(AgentPlan plan)
    {
        if (!plan.RequiresApproval)
        {
            return false;
        }

        if (!_options.RequireApprovalForDestructiveActions)
        {
            return false;
        }

        return true;
    }
}
