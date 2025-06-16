using Microsoft.Data.SqlClient;
using MSSQL.MCP.Configuration;

namespace MSSQL.MCP.Database;

public class SqlConnectionFactory(IOptions<DatabaseOptions> databaseOptions) : ISqlConnectionFactory
{
    private readonly string _connectionString = databaseOptions.Value.ConnectionString;

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection();
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await CreateOpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
} 