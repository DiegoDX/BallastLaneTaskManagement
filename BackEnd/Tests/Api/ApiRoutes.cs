namespace Tests.Api;

internal static class ApiRoutes
{
    public const string Register = "/api/v1/auth/register";
    public const string Login = "/api/v1/auth/login";
    public const string Tasks = "/api/v1/tasks";

    public static string TaskById(Guid taskId) => $"/api/v1/tasks/{taskId}";
}
