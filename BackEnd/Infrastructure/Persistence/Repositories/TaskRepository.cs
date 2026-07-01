using System.Data;
using System.Text;
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
            SELECT Id, UserId, Title, Description, Status, DueDate, CreatedAtUtc
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
            SELECT Id, UserId, Title, Description, Status, DueDate, CreatedAtUtc
            FROM Tasks
            WHERE UserId = @UserId
            ORDER BY CreatedAtUtc DESC;
            """;

        return await QueryListAsync(
            sql,
            command => AddGuidParameter(command, "@UserId", userId),
            cancellationToken);
    }

    public async Task<(IReadOnlyList<TaskItem> Items, int TotalRecords)> SearchAsync(
        TaskSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var whereClause = BuildWhereClause(criteria, out var parameters);
        var sortColumn = MapSortColumn(criteria.SortBy);
        var sortDirection = criteria.SortDirection == SortDirection.Asc ? "ASC" : "DESC";
        var offset = (criteria.PageNumber - 1) * criteria.PageSize;

        var countSql = $"""
            SELECT COUNT(1)
            FROM Tasks
            WHERE {whereClause};
            """;

        var dataSql = $"""
            SELECT Id, UserId, Title, Description, Status, DueDate, CreatedAtUtc
            FROM Tasks
            WHERE {whereClause}
            ORDER BY {sortColumn} {sortDirection}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var connection = await _connectionFactory
            .CreateConnectionAsync(cancellationToken);

        int totalRecords;
        try
        {
            await using var countCommand = new SqlCommand(countSql, connection);
            ApplySearchParameters(countCommand, parameters);
            var countResult = await countCommand.ExecuteScalarAsync(cancellationToken);
            totalRecords = Convert.ToInt32(countResult);
        }
        catch (SqlException ex)
        {
            throw new DataAccessException("Failed to count task data.", ex);
        }

        if (totalRecords == 0)
        {
            return (Array.Empty<TaskItem>(), 0);
        }

        try
        {
            await using var dataCommand = new SqlCommand(dataSql, connection);
            ApplySearchParameters(dataCommand, parameters);
            AddIntParameter(dataCommand, "@Offset", offset);
            AddIntParameter(dataCommand, "@PageSize", criteria.PageSize);

            await using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);
            var tasks = new List<TaskItem>();

            while (await reader.ReadAsync(cancellationToken))
            {
                tasks.Add(MapTaskItem(reader));
            }

            return (tasks, totalRecords);
        }
        catch (SqlException ex)
        {
            throw new DataAccessException("Failed to query task data.", ex);
        }
    }

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
        => CreateAsync(task, cancellationToken);

    public async Task CreateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        const string sql = """
            INSERT INTO Tasks (Id, UserId, Title, Description, Status, DueDate, CreatedAtUtc)
            VALUES (@Id, @UserId, @Title, @Description, @Status, @DueDate, @CreatedAtUtc);
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
                AddDateTimeParameter(command, "@CreatedAtUtc", task.CreatedAtUtc);
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
                DueDate = @DueDate,
                CreatedAtUtc = @CreatedAtUtc
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
                AddDateTimeParameter(command, "@CreatedAtUtc", task.CreatedAtUtc);
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

    private static string BuildWhereClause(
        TaskSearchCriteria criteria,
        out List<SqlParameter> parameters)
    {
        parameters =
        [
            new("@UserId", SqlDbType.UniqueIdentifier) { Value = criteria.UserId }
        ];

        var builder = new StringBuilder("UserId = @UserId");

        if (!string.IsNullOrWhiteSpace(criteria.TitleContains))
        {
            builder.Append(" AND Title LIKE @TitleContains");
            parameters.Add(new SqlParameter("@TitleContains", SqlDbType.NVarChar, 256)
            {
                Value = $"%{criteria.TitleContains}%"
            });
        }

        if (criteria.Status is not null)
        {
            builder.Append(" AND Status = @Status");
            parameters.Add(new SqlParameter("@Status", SqlDbType.Int)
            {
                Value = (int)criteria.Status.Value
            });
        }

        return builder.ToString();
    }

    private static string MapSortColumn(TaskSortField sortBy) =>
        sortBy switch
        {
            TaskSortField.Title => "Title",
            TaskSortField.Status => "Status",
            TaskSortField.CreatedDate => "CreatedAtUtc",
            _ => "CreatedAtUtc"
        };

    private static void ApplySearchParameters(SqlCommand command, IEnumerable<SqlParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.Add((SqlParameter)((ICloneable)parameter).Clone());
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
        var createdAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"));

        return TaskItem.Restore(id, userId, title, description, status, dueDate, createdAtUtc);
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
