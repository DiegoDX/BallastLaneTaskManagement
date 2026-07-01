using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Tests.Infrastructure;

namespace Tests.Api;

public sealed class ApiIntegrationFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = [];

    public HttpClient HttpClient { get; private set; } = null!;

    public HttpClient CreateIsolatedClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }

    public async Task InitializeAsync()
    {
        var configuration = BuildApiConfiguration();

        try
        {
            await DatabaseSchemaInitializer.EnsureSchemaAsync(configuration);
            IntegrationTestEnvironment.IsDatabaseAvailable = true;
            IntegrationTestEnvironment.UnavailableReason = string.Empty;
        }
        catch (Exception ex)
        {
            IntegrationTestEnvironment.IsDatabaseAvailable = false;
            IntegrationTestEnvironment.UnavailableReason =
                $"SQL Server integration database is not available: {ex.Message}";
        }

        HttpClient = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public new async Task DisposeAsync()
    {
        foreach (var userId in _createdUserIds)
        {
            await CleanupUserAsync(userId);
        }

        HttpClient.Dispose();
        await base.DisposeAsync();
    }

    public void TrackUser(Guid userId)
    {
        _createdUserIds.Add(userId);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "appsettings.api.integration.json"),
                optional: false);
        });
    }

    private static IConfiguration BuildApiConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.api.integration.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private async Task CleanupUserAsync(Guid userId)
    {
        if (!IntegrationTestEnvironment.IsDatabaseAvailable)
        {
            return;
        }

        var configuration = BuildApiConfiguration();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = """
            DELETE FROM Tasks WHERE UserId = @UserId;
            DELETE FROM RefreshTokens WHERE UserId = @UserId;
            DELETE FROM Users WHERE Id = @UserId;
            """;

        await using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", System.Data.SqlDbType.UniqueIdentifier).Value = userId;
        await command.ExecuteNonQueryAsync();
    }
}
