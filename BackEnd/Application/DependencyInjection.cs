using Application.Agent;
using Application.Agent.Phases;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Llm.TaskAssistant;
using Application.Rag;
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

        services.Configure<AgentOptions>(
            configuration.GetSection(AgentOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ITaskSuggestionService, TaskSuggestionService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ITaskAssistantService, TaskAssistantService>();
        services.AddScoped<ITaskToolExecutor, TaskToolExecutor>();
        services.AddScoped<IDocAssistantService, DocAssistantService>();
        services.AddScoped<IRagRetriever, RagRetriever>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();
        services.AddScoped<IAgentPhaseHandler, PlanPhaseHandler>();
        services.AddScoped<IAgentPhaseHandler, ApprovalPhaseHandler>();
        services.AddScoped<IAgentPhaseHandler, ExecutePhaseHandler>();
        services.AddScoped<IAgentPhaseHandler, ReviewPhaseHandler>();
        services.AddScoped<IAgentPhaseHandler, SummaryPhaseHandler>();

        return services;
    }
}
