namespace Application.DTOs.Agent;

public sealed record AgentPlan(
    string Goal,
    IReadOnlyList<AgentPlanStep> Steps,
    bool RequiresApproval,
    string RiskLevel);
