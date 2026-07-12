namespace Application.DTOs.Agent;

public sealed record AgentReview(
    bool Success,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Recommendations,
    bool RequiresReExecution = false,
    string? ReExecutionHint = null);
