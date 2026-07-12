using Application.DTOs.Agent.Specialists;

namespace Application.Agent.Specialists;

public interface ISummaryAgent
{
    Task<SummaryAgentResult> SummarizeAsync(
        SummaryAgentRequest request,
        CancellationToken cancellationToken = default);
}
