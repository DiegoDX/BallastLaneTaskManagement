using System.Data;
using Application.Interfaces.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Exceptions;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public RefreshTokenRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);

        const string sql = """
            INSERT INTO RefreshTokens
                (Id, UserId, TokenHash, ExpiresAtUtc, CreatedAtUtc, RevokedAtUtc, ReplacedByTokenHash)
            VALUES
                (@Id, @UserId, @TokenHash, @ExpiresAtUtc, @CreatedAtUtc, @RevokedAtUtc, @ReplacedByTokenHash);
            """;

        await ExecuteNonQueryAsync(
            sql,
            command =>
            {
                AddGuidParameter(command, "@Id", refreshToken.Id);
                AddGuidParameter(command, "@UserId", refreshToken.UserId);
                AddStringParameter(command, "@TokenHash", refreshToken.TokenHash, size: 128);
                AddDateTimeParameter(command, "@ExpiresAtUtc", refreshToken.ExpiresAtUtc);
                AddDateTimeParameter(command, "@CreatedAtUtc", refreshToken.CreatedAtUtc);
                AddNullableDateTimeParameter(command, "@RevokedAtUtc", refreshToken.RevokedAtUtc);
                AddNullableStringParameter(command, "@ReplacedByTokenHash", refreshToken.ReplacedByTokenHash, size: 128);
            },
            cancellationToken);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        const string sql = """
            SELECT Id, UserId, TokenHash, ExpiresAtUtc, CreatedAtUtc, RevokedAtUtc, ReplacedByTokenHash
            FROM RefreshTokens
            WHERE TokenHash = @TokenHash;
            """;

        return await QuerySingleAsync(
            sql,
            command => AddStringParameter(command, "@TokenHash", tokenHash, size: 128),
            cancellationToken);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);

        const string sql = """
            UPDATE RefreshTokens
            SET RevokedAtUtc = @RevokedAtUtc,
                ReplacedByTokenHash = @ReplacedByTokenHash
            WHERE Id = @Id;
            """;

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            command =>
            {
                AddGuidParameter(command, "@Id", refreshToken.Id);
                AddNullableDateTimeParameter(command, "@RevokedAtUtc", refreshToken.RevokedAtUtc);
                AddNullableStringParameter(
                    command,
                    "@ReplacedByTokenHash",
                    refreshToken.ReplacedByTokenHash,
                    size: 128);
            },
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new DataAccessException(
                $"Refresh token with id '{refreshToken.Id}' was not found for update.",
                new InvalidOperationException("No rows were affected."));
        }
    }

    private async Task<RefreshToken?> QuerySingleAsync(
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

            return MapRefreshToken(reader);
        }
        catch (SqlException ex)
        {
            throw new DataAccessException("Failed to query refresh token data.", ex);
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
            throw new DataAccessException("Failed to persist refresh token data.", ex);
        }
    }

    private static RefreshToken MapRefreshToken(SqlDataReader reader)
    {
        var id = reader.GetGuid(reader.GetOrdinal("Id"));
        var userId = reader.GetGuid(reader.GetOrdinal("UserId"));
        var tokenHash = reader.GetString(reader.GetOrdinal("TokenHash"));
        var expiresAtUtc = reader.GetDateTime(reader.GetOrdinal("ExpiresAtUtc"));
        var createdAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"));
        var revokedAtUtc = reader.IsDBNull(reader.GetOrdinal("RevokedAtUtc"))
            ? (DateTime?)null
            : reader.GetDateTime(reader.GetOrdinal("RevokedAtUtc"));
        var replacedByTokenHash = reader.IsDBNull(reader.GetOrdinal("ReplacedByTokenHash"))
            ? null
            : reader.GetString(reader.GetOrdinal("ReplacedByTokenHash"));

        return RefreshToken.Restore(
            id,
            userId,
            tokenHash,
            expiresAtUtc,
            createdAtUtc,
            revokedAtUtc,
            replacedByTokenHash);
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

    private static void AddNullableStringParameter(SqlCommand command, string name, string? value, int size)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
        parameter.Value = (object?)value ?? DBNull.Value;
    }

    private static void AddDateTimeParameter(SqlCommand command, string name, DateTime value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.DateTime2);
        parameter.Value = value;
    }

    private static void AddNullableDateTimeParameter(SqlCommand command, string name, DateTime? value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.DateTime2);
        parameter.Value = value.HasValue ? value.Value : DBNull.Value;
    }
}
