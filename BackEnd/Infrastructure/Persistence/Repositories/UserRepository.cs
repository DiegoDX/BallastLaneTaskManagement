using System.Data;
using Application.Interfaces.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Exceptions;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public UserRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, PasswordHash
            FROM Users
            WHERE Id = @Id;
            """;

        return QuerySingleAsync(
            sql,
            command => AddGuidParameter(command, "@Id", id),
            cancellationToken);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        const string sql = """
            SELECT Id, Name, PasswordHash
            FROM Users
            WHERE Name = @Username;
            """;

        return QuerySingleAsync(
            sql,
            command => AddStringParameter(command, "@Username", username.Trim(), size: 256),
            cancellationToken);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
        => CreateAsync(user, cancellationToken);

    public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        const string sql = """
            INSERT INTO Users (Id, Name, PasswordHash)
            VALUES (@Id, @Name, @PasswordHash);
            """;

        await ExecuteNonQueryAsync(
            sql,
            command =>
            {
                AddGuidParameter(command, "@Id", user.Id);
                AddStringParameter(command, "@Name", user.Name.Value, size: 256);
                AddStringParameter(command, "@PasswordHash", user.PasswordHash.Value, size: 512);
            },
            cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        const string sql = """
            UPDATE Users
            SET Name = @Name,
                PasswordHash = @PasswordHash
            WHERE Id = @Id;
            """;

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            command =>
            {
                AddGuidParameter(command, "@Id", user.Id);
                AddStringParameter(command, "@Name", user.Name.Value, size: 256);
                AddStringParameter(command, "@PasswordHash", user.PasswordHash.Value, size: 512);
            },
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new DataAccessException(
                $"User with id '{user.Id}' was not found for update.",
                new InvalidOperationException("No rows were affected."));
        }
    }

    private async Task<User?> QuerySingleAsync(
        string sql,
        Action<SqlCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .CreateConnectionAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        configureCommand(command);

        try
        {
            await using var reader = await command
                .ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return MapUser(reader);
        }
        catch (SqlException ex)
        {
            throw new DataAccessException("Failed to query user data.", ex);
        }
    }

    private async Task<int> ExecuteNonQueryAsync(
        string sql,
        Action<SqlCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .CreateConnectionAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        configureCommand(command);

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            throw new DataAccessException("Failed to persist user data.", ex);
        }
    }

    private static User MapUser(SqlDataReader reader)
    {
        var id = reader.GetGuid(reader.GetOrdinal("Id"));
        var name = reader.GetString(reader.GetOrdinal("Name"));
        var passwordHash = reader.GetString(reader.GetOrdinal("PasswordHash"));

        return User.Restore(id, name, passwordHash);
    }

    private static void AddGuidParameter(SqlCommand command, string name, Guid value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.UniqueIdentifier);
        parameter.Value = value;
    }

    private static void AddStringParameter(SqlCommand command, string name, string value, int size)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
        parameter.Value = value;
    }
}
