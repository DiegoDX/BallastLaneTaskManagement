namespace Infrastructure.Mcp;

public static class McpUserContext
{
    public const string UserIdEnvironmentVariable = "BALLASTLANE_USER_ID";

    public static Guid GetCurrentUserId()
    {
        var value = Environment.GetEnvironmentVariable(UserIdEnvironmentVariable);

        if (!Guid.TryParse(value, out var userId) || userId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"{UserIdEnvironmentVariable} must be set to a valid user GUID.");
        }

        return userId;
    }
}
