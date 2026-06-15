using Infrastructure.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    public const string DefaultConnectionStringName = "DefaultConnection";

    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _connectionString = configuration.GetConnectionString(DefaultConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{DefaultConnectionStringName}' is not configured.");
    }

    public async Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (SqlException ex)
        {
            await connection.DisposeAsync();
            throw new DataAccessException("Failed to open a database connection.", ex);
        }
    }
}
