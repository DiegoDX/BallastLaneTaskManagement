namespace Infrastructure.Configuration;

public sealed class McpSettings
{
    public const string SectionName = "Mcp";

    public string ServerProjectPath { get; init; } =
        "../Mcp/BallastLane.TaskAssistant.Mcp.Server/BallastLane.TaskAssistant.Mcp.Server.csproj";

    public int StartupTimeoutSeconds { get; init; } = 10;

    public bool SessionPerRequest { get; init; } = true;
}
