namespace Application.Agent;

public sealed record AgentPhaseOutcome(
    string Status,
    string? OutputJson = null,
    bool ShouldStop = false);
