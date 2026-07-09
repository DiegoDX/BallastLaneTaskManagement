using Application.Interfaces;
using Application.Interfaces.Repositories;
using Infrastructure.Configuration;
using Infrastructure.Data;
using Infrastructure.Llm;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services
            .AddOptions<LlmSettings>()
            .Bind(configuration.GetSection(LlmSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<LlmSettings>, LlmSettingsValidator>();
        services.AddHttpClient<OllamaLlmClient>();
        services.AddSingleton<OpenAiLlmClient>();
        services.AddSingleton<ILlmClient, LlmClientFactory>();

        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddSingleton<IAuthTokenService, JwtAuthTokenService>();

        return services;
    }
}
