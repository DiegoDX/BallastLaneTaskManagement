namespace Application.DTOs.Agent;

public sealed record AgentPlanStep(int Order, string Description, string? ToolHint = null);
