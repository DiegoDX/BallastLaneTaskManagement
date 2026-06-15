using Infrastructure.Data;using Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit.Sdk;

namespace Tests.Infrastructure;

public sealed class IntegrationDatabaseFixture : IAsyncLifetime
{
    public bool IsAvailable { get; private set; }

    public string UnavailableReason { get; private set; } = "SQL Server integration database is not available.";

    public IConfiguration Configuration { get; private set; } = null!;

    public ISqlConnectionFactory ConnectionFactory { get; private set; } = null!;

    public UserRepository UserRepository { get; private set; } = null!;

    public TaskRepository TaskRepository { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Configuration = IntegrationTestConfiguration.Build();
        ConnectionFactory = new SqlConnectionFactory(Configuration);
        UserRepository = new UserRepository(ConnectionFactory);
        TaskRepository = new TaskRepository(ConnectionFactory);

        try
        {
            await DatabaseSchemaInitializer.EnsureSchemaAsync(Configuration);
            await using var connection = await ConnectionFactory.CreateConnectionAsync();
            UserRepository = new UserRepository(ConnectionFactory);
            TaskRepository = new TaskRepository(ConnectionFactory);
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            throw SkipException.ForSkip($"Integration DB not available: {ex.Message}");
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task CleanupUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            DELETE FROM Tasks WHERE UserId = @UserId;
            DELETE FROM Users WHERE Id = @UserId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", System.Data.SqlDbType.UniqueIdentifier).Value = userId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CleanupTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = "DELETE FROM Tasks WHERE Id = @TaskId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TaskId", System.Data.SqlDbType.UniqueIdentifier).Value = taskId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
