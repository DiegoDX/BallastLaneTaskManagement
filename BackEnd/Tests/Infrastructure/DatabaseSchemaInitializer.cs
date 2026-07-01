using Infrastructure.Data;
using Infrastructure.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Tests.Infrastructure;

internal static class DatabaseSchemaInitializer
{
    public static async Task EnsureSchemaAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString(SqlConnectionFactory.DefaultConnectionStringName)
            ?? throw new InvalidOperationException(
                "Integration test connection string 'DefaultConnection' is not configured.");

        var targetBuilder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = targetBuilder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "The connection string must specify an Initial Catalog (database name).");
        }

        await EnsureDatabaseExistsAsync(targetBuilder, databaseName, cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);


        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users')
            BEGIN
                CREATE TABLE Users
                (
                    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    Name NVARCHAR(256) NOT NULL,
                    PasswordHash NVARCHAR(512) NOT NULL
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_Name' AND object_id = OBJECT_ID('Users'))
            BEGIN
                CREATE UNIQUE INDEX UX_Users_Name ON Users(Name);
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Tasks')
            BEGIN
                CREATE TABLE Tasks
                (
                    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    Title NVARCHAR(256) NOT NULL,
                    Description NVARCHAR(MAX) NULL,
                    Status INT NOT NULL,
                    DueDate DATETIME2 NOT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Tasks_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_Tasks_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RefreshTokens' AND schema_id = SCHEMA_ID('dbo'))
            BEGIN
                CREATE TABLE RefreshTokens
                (
                    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    TokenHash NVARCHAR(128) NOT NULL,
                    ExpiresAtUtc DATETIME2 NOT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL,
                    RevokedAtUtc DATETIME2 NULL,
                    ReplacedByTokenHash NVARCHAR(128) NULL,
                    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
                );
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'UX_RefreshTokens_TokenHash' AND object_id = OBJECT_ID('RefreshTokens'))
            BEGIN
                CREATE UNIQUE INDEX UX_RefreshTokens_TokenHash ON RefreshTokens (TokenHash);
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_RefreshTokens_UserId' AND object_id = OBJECT_ID('RefreshTokens'))
            BEGIN
                CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens (UserId);
            END;
            """;

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureTasksCreatedAtUtcColumnAsync(connection, cancellationToken);
    }

    private static async Task EnsureTasksCreatedAtUtcColumnAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string addColumnSql = """
            IF COL_LENGTH(N'dbo.Tasks', N'CreatedAtUtc') IS NULL
            BEGIN
                ALTER TABLE Tasks ADD CreatedAtUtc DATETIME2 NULL;
            END;
            """;

        const string backfillColumnSql = """
            IF COL_LENGTH(N'dbo.Tasks', N'CreatedAtUtc') IS NOT NULL
            BEGIN
                UPDATE Tasks SET CreatedAtUtc = SYSUTCDATETIME() WHERE CreatedAtUtc IS NULL;
                ALTER TABLE Tasks ALTER COLUMN CreatedAtUtc DATETIME2 NOT NULL;
            END;
            """;

        await using (var addColumnCommand = new SqlCommand(addColumnSql, connection))
        {
            await addColumnCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var backfillCommand = new SqlCommand(backfillColumnSql, connection);
        await backfillCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureDatabaseExistsAsync(
        SqlConnectionStringBuilder targetBuilder,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var masterBuilder = new SqlConnectionStringBuilder(targetBuilder.ConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var masterConnection = new SqlConnection(masterBuilder.ConnectionString);

        try
        {
            await masterConnection.OpenAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            throw new DataAccessException(
                "Failed to connect to the SQL Server 'master' database during initialization.",
                ex);
        }

        await EnsureDatabaseExistsInternalAsync(
        masterConnection,
        databaseName,
        cancellationToken);
    }

    private static async Task EnsureDatabaseExistsInternalAsync(
    SqlConnection masterConnection,
    string databaseName,
    CancellationToken cancellationToken)
    {
        if (await DatabaseExistsAsync(
                masterConnection,
                databaseName,
                cancellationToken))
        {
            return;
        }

        var escapedDatabaseName = EscapeIdentifier(databaseName);

        var sql = $"CREATE DATABASE {databaseName};";

        try
        {
            await using var command = new SqlCommand(sql, masterConnection)
            {
                CommandTimeout = 120
            };

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            throw new DataAccessException(
                $"Failed to create database '{databaseName}'.",
                ex);
        }
    }

    private static async Task<bool> DatabaseExistsAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CASE
                WHEN EXISTS (SELECT 1 FROM sys.databases WHERE name = @DatabaseName) THEN 1
                ELSE 0
            END;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@DatabaseName", System.Data.SqlDbType.NVarChar, 128).Value = databaseName;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    private static string EscapeIdentifier(string identifier) =>
       $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
