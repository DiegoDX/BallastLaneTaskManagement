using System.Text.RegularExpressions;
using Infrastructure.Data;
using Infrastructure.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

public static class DatabaseInitializer
{
    private const string SchemaScriptResourceName = "Infrastructure.Persistence.Scripts.schema.sql";
    private const string SeedScriptResourceName = "Infrastructure.Persistence.Scripts.seed.sql";

    private static readonly Regex ValidDatabaseNamePattern = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static async Task InitializeAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(SqlConnectionFactory.DefaultConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{SqlConnectionFactory.DefaultConnectionStringName}' is not configured.");

        var targetBuilder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = targetBuilder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "The connection string must specify an Initial Catalog (database name).");
        }

        ValidateDatabaseName(databaseName);

        await EnsureDatabaseExistsAsync(targetBuilder, databaseName, cancellationToken);

        await using var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            throw new DataAccessException(
                $"Failed to connect to database '{databaseName}' during initialization.",
                ex);
        }

        var schemaScript = await LoadScriptAsync(SchemaScriptResourceName, "schema.sql", cancellationToken);
        await SqlScriptExecutor.ExecuteAsync(connection, schemaScript, "schema.sql", cancellationToken);

        var seedScript = await LoadScriptAsync(SeedScriptResourceName, "seed.sql", cancellationToken);
        await SqlScriptExecutor.ExecuteAsync(connection, seedScript, "seed.sql", cancellationToken);
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

        var sql = $"CREATE DATABASE {escapedDatabaseName};";

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

    private static async Task<string> LoadScriptAsync(
        string embeddedResourceName,
        string fileName,
        CancellationToken cancellationToken)
    {
        var assembly = typeof(DatabaseInitializer).Assembly;

        await using var stream = assembly.GetManifestResourceStream(embeddedResourceName);
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        var filePath = Path.Combine(
            Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory,
            "Persistence",
            "Scripts",
            fileName);

        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"SQL script '{fileName}' was not found as embedded resource '{embeddedResourceName}' " +
                $"or on disk at '{filePath}'.");
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    private static void ValidateDatabaseName(string databaseName)
    {
        if (!ValidDatabaseNamePattern.IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                $"Database name '{databaseName}' is invalid. Only letters, digits, and underscores are allowed, " +
                "and the name must start with a letter or underscore.");
        }
    }

    private static string EscapeIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
