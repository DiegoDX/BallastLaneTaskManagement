using Microsoft.Data.SqlClient;

namespace Infrastructure.Data;

public interface ISqlConnectionFactory
{
    Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
