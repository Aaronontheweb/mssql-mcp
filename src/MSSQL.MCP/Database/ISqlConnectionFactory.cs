using Microsoft.Data.SqlClient;

namespace MSSQL.MCP.Database;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
    Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
} 