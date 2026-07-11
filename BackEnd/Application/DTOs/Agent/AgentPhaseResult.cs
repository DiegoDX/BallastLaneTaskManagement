namespace Application.DTOs.Agent;

public sealed record AgentPhaseResult(
    string Phase,
    string Status,
    string? OutputJson,
    int? DurationMs);
