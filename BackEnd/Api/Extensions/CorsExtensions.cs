using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Extensions;

public static class CorsExtensions
{
    private static readonly string[] AllowedMethods =
    [
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "OPTIONS"
    ];

    private static readonly string[] AllowedHeaders =
    [
        "Authorization",
        "Content-Type",
        "Accept"
    ];

    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();
        services.AddCors();

        return services;
    }

    public static WebApplication UseApiCors(this WebApplication app)
    {
        var corsSettings = app.Services.GetRequiredService<IOptions<CorsSettings>>().Value;

        app.UseCors(policy =>
        {
            if (corsSettings.AllowedOrigins.Length > 0)
            {
                policy.WithOrigins(corsSettings.AllowedOrigins);
            }

            policy
                .WithMethods(AllowedMethods)
                .WithHeaders(AllowedHeaders)
                .AllowCredentials();
        });

        return app;
    }
}
