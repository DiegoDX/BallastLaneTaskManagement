using Application.DTOs.Agent.Specialists;

namespace Application.Agent.Specialists;

public interface IReviewerAgent
{
    Task<ReviewerAgentResult> ReviewAsync(
        ReviewerAgentRequest request,
        CancellationToken cancellationToken = default);
}
