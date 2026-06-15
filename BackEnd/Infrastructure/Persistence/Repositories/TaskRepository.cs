using System.Data;
using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Exceptions;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Persistence.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TaskRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, UserId, Title, Description, Status, DueDate
            FROM Tasks
            WHERE Id = @Id;
            """;

        return QuerySingleAsync(
            sql,
            command => AddGuidParameter(command, "@Id", id),
            cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, UserId, Title, Description, Status, DueDate
            FROM Tasks
            WHERE UserId = @UserId
            ORDER BY DueDate ASC;
            """;

        return await QueryListAsync(
            sql,
            command => AddGuidParameter(command, "@UserId", userId),
            cancellationToken);
    }

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
        => CreateAsync(task, cancellationToken);

    public async Task CreateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        const string sql = """
            INSERT INTO Tasks (Id, UserId, Title, Description, Status, DueDate)
            VALUES (@Id, @UserId, @Title, @Description, @Status, @DueDate);
            """;

        await ExecuteNonQueryAsync(
            sql,
            command =>
            {
                AddGuidParameter(command, "@Id", task.Id);
                AddGuidParameter(command, "@UserId", task.UserId);
                AddStringParameter(command, "@Title", task.Title.Value, size: 256);
                AddDescriptionParameter(command, "@Description", task.Description);
                AddIntParameter(command, "@Status", (int)task.Status);
                AddDateTimeParameter(command, "@DueDate", task.DueDate.Value);
            },
            cancellationToken);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        const string sql = """
            UPDATE Tasks
            SET UserId = @UserId,
                Title = @Title,
                Description = @Description,
                Status = @Status,
                DueDate = @DueDate
            WHERE Id = @Id;
            """;

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            command =>
            {
                AddGuidParameter(command, "@Id", task.Id);
                AddGuidParameter(command, "@UserId", task.UserId);
                AddStringParameter(command, "@Title", task.Title.Value, size: 256);
                AddDescriptionParameter(command, "@Description", task.Description);
                AddIntParameter(command, "@Status", (int)task.Status);
                AddDateTimeParameter(command, "@DueDate", task.DueDate.Value);
            },
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new DataAccessException(
                $"Task with id '{task.Id}' was not found for update.",
                new InvalidOperationException("No rows were affected."));
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM Tasks
            WHERE Id = @Id;
            """;

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            command => AddGuidParameter(command, "@Id", id),
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new DataAccessException(
                $"Task with id '{id}' was not found for deletion.",
                new InvalidOperationException("No rows were affected."));
        }
    }

    private async Task<TaskItem?> QuerySingleAsync(
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

            return MapTaskItem(reader);
        }
        catch (SqlException ex)
        {
            throw new DataAccessException("Failed to query task data.", ex);
        }
    }

    private async Task<IReadOnlyList<TaskItem>> QueryListAsync(
        string sql,
        Action<SqlCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var command = new SqlCommand(sql, connection);
        configureCommand(command);

        try
        {
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken);

            var tasks = new List<TaskItem>();

            while (await reader.ReadAsync(cancellationToken))
            {
                tasks.Add(MapTaskItem(reader));
            }

            return tasks;
        }
        catch (SqlException ex)
        {
            throw new DataAccessException("Failed to query task data.", ex);
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
            throw new DataAccessException("Failed to persist task data.", ex);
        }
    }

    private static TaskItem MapTaskItem(SqlDataReader reader)
    {
        var id = reader.GetGuid(reader.GetOrdinal("Id"));
        var userId = reader.GetGuid(reader.GetOrdinal("UserId"));
        var title = reader.GetString(reader.GetOrdinal("Title"));

        var descriptionOrdinal = reader.GetOrdinal("Description");
        var description = reader.IsDBNull(descriptionOrdinal)
            ? null
            : reader.GetString(descriptionOrdinal);

        var status = (TaskItemStatus)reader.GetInt32(reader.GetOrdinal("Status"));
        var dueDate = reader.GetDateTime(reader.GetOrdinal("DueDate"));

        return TaskItem.Restore(id, userId, title, description, status, dueDate);
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

    private static void AddDescriptionParameter(SqlCommand command, string name, string? value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, -1);
        parameter.Value = value is null ? DBNull.Value : value;
    }

    private static void AddIntParameter(SqlCommand command, string name, int value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Int);
        parameter.Value = value;
    }

    private static void AddDateTimeParameter(SqlCommand command, string name, DateTime value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.DateTime2);
        parameter.Value = value;
    }
}
