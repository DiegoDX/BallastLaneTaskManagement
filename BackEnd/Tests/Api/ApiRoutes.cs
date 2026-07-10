namespace Tests.Api;

internal static class ApiRoutes
{
    public const string Register = "/api/v1/auth/register";
    public const string Login = "/api/v1/auth/login";
    public const string Refresh = "/api/v1/auth/refresh";
    public const string Logout = "/api/v1/auth/logout";
    public const string Tasks = "/api/v1/tasks";
    public const string TaskSuggestions = "/api/v1/tasks/suggestions";
    public const string TaskSuggestionsGenerate = "/api/v1/tasks/suggestions/generate";
    public const string TaskSuggestionsCreate = "/api/v1/tasks/suggestions/create";
    public const string Chat = "/api/v1/chat";
    public const string TaskAssistant = "/api/v1/task-assistant";

    public static string TaskById(Guid taskId) => $"/api/v1/tasks/{taskId}";
}
