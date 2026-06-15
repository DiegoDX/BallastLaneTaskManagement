namespace Tests.Infrastructure;

public static class IntegrationTestEnvironment
{
    public static bool IsDatabaseAvailable { get; internal set; }

    public static string UnavailableReason { get; internal set; } =
        "SQL Server integration database is not available.";
}
