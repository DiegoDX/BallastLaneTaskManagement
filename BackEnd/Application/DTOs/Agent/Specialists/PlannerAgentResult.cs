namespace Application.DTOs.Agent.Specialists;

public sealed record PlannerAgentResult(
    AgentPlan Plan,
    string? Model);
