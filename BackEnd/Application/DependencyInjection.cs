using Application.Interfaces.Services;
using Application.Llm.TaskAssistant;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RefreshTokenOptions>(
            configuration.GetSection(RefreshTokenOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ITaskSuggestionService, TaskSuggestionService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ITaskAssistantService, TaskAssistantService>();
        services.AddScoped<ITaskToolExecutor, TaskToolExecutor>();

        return services;
    }
}
