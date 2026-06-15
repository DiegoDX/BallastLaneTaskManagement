using Microsoft.Extensions.Configuration;

namespace Tests.Infrastructure;

internal static class IntegrationTestConfiguration
{
    public static IConfiguration Build()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.integration.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
