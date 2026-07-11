namespace Application.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int MaxExecuteIterations { get; init; } = 10;
    public int MaxToolCallsPerIteration { get; init; } = 5;
    public int MaxPhaseRetries { get; init; } = 2;
    public int RunTtlMinutes { get; init; } = 30;
    public bool RequireApprovalForDestructiveActions { get; init; } = true;
    public int BulkUpdateApprovalThreshold { get; init; } = 3;
}
