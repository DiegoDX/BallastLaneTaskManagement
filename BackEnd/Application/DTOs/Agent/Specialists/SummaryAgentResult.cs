namespace Application.DTOs.Agent.Specialists;

public sealed record SummaryAgentResult(
    string Summary,
    string? OutputJson,
    string? Model);
