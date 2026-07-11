namespace Application.DTOs.Agent;

public sealed record AgentExecutionReport(
    int Iterations,
    IReadOnlyList<AgentToolCallRecord> ToolCalls);
