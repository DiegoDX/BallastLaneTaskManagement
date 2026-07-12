namespace Application.DTOs.Agent.Specialists;

public sealed record ReviewerAgentResult(
    AgentReview Review,
    string? Model);
