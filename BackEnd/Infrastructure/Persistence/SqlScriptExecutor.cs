using System.Text;
using Infrastructure.Exceptions;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Persistence;

internal static class SqlScriptExecutor
{
    public static async Task ExecuteAsync(
        SqlConnection connection,
        string script,
        string scriptName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptName);

        var batches = SplitByGo(script);

        for (var index = 0; index < batches.Count; index++)
        {
            var batch = batches[index].Trim();
            if (batch.Length == 0)
            {
                continue;
            }

            try
            {
                await using var command = new SqlCommand(batch, connection)
                {
                    CommandTimeout = 120
                };

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex)
            {
                throw new DataAccessException(
                    $"Failed to execute SQL script '{scriptName}' (batch {index + 1} of {batches.Count}).",
                    ex);
            }
        }
    }

    internal static IReadOnlyList<string> SplitByGo(string script)
    {
        var batches = new List<string>();
        var currentBatch = new StringBuilder();

        using var reader = new StringReader(script);
        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (currentBatch.Length > 0)
                {
                    batches.Add(currentBatch.ToString());
                    currentBatch.Clear();
                }

                continue;
            }

            currentBatch.AppendLine(line);
        }

        if (currentBatch.Length > 0)
        {
            batches.Add(currentBatch.ToString());
        }

        return batches;
    }
}
