using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using MSSQL.MCP.Database;

namespace MSSQL.MCP.Tools;

[McpServerToolType]
public class SqlExecutionTool
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlExecutionTool(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [McpServerTool, Description("Execute SQL queries against the connected MSSQL database. Supports SELECT, INSERT, UPDATE, DELETE, and DDL operations.")]
    public async Task<string> ExecuteSql(
        [Description("The SQL query to execute")] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Error: SQL query cannot be empty";
        }

        try
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            using var command = new SqlCommand(query, connection);
            
            // Determine if this is a SELECT query or a command
            var trimmedQuery = query.Trim();
            var isSelectQuery = trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                               trimmedQuery.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);

            if (isSelectQuery)
            {
                // Handle SELECT queries - return data
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                return await FormatQueryResults(reader, cancellationToken);
            }
            else
            {
                // Handle INSERT/UPDATE/DELETE/DDL - return affected rows
                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                return $"Query executed successfully. Rows affected: {rowsAffected}";
            }
        }
        catch (SqlException ex)
        {
            return $"SQL Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool, Description("List all tables in the database with basic information.")]
    public async Task<string> ListTables(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            
            var query = @"
                SELECT 
                    t.TABLE_SCHEMA,
                    t.TABLE_NAME,
                    t.TABLE_TYPE,
                    ISNULL(p.rows, 0) as ROW_COUNT
                FROM INFORMATION_SCHEMA.TABLES t
                LEFT JOIN (
                    SELECT 
                        SCHEMA_NAME(o.schema_id) as schema_name,
                        o.name as table_name,
                        SUM(p.rows) as rows
                    FROM sys.objects o
                    JOIN sys.partitions p ON o.object_id = p.object_id
                    WHERE o.type = 'U' AND p.index_id IN (0,1)
                    GROUP BY o.schema_id, o.name
                ) p ON t.TABLE_SCHEMA = p.schema_name AND t.TABLE_NAME = p.table_name
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            return await FormatQueryResults(reader, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Error listing tables: {ex.Message}";
        }
    }

    [McpServerTool, Description("Describe the structure of a specific table including columns, data types, and constraints.")]
    public async Task<string> DescribeTable(
        [Description("The name of the table to describe (can include schema, e.g., 'dbo.Users')")] string tableName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return "Error: Table name cannot be empty";
        }

        try
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            
            // Parse schema and table name
            var parts = tableName.Split('.');
            var schema = parts.Length > 1 ? parts[0] : "dbo";
            var table = parts.Length > 1 ? parts[1] : parts[0];

            var query = @"
                SELECT 
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE,
                    c.IS_NULLABLE,
                    c.COLUMN_DEFAULT,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END as IS_PRIMARY_KEY,
                    CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END as IS_FOREIGN_KEY,
                    fk.REFERENCED_TABLE_SCHEMA + '.' + fk.REFERENCED_TABLE_NAME + '(' + fk.REFERENCED_COLUMN_NAME + ')' as REFERENCES
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN (
                    SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku 
                        ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME 
                        AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                     AND c.TABLE_NAME = pk.TABLE_NAME 
                     AND c.COLUMN_NAME = pk.COLUMN_NAME
                LEFT JOIN (
                    SELECT 
                        ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME,
                        ref_ku.TABLE_SCHEMA as REFERENCED_TABLE_SCHEMA,
                        ref_ku.TABLE_NAME as REFERENCED_TABLE_NAME,
                        ref_ku.COLUMN_NAME as REFERENCED_COLUMN_NAME
                    FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku 
                        ON rc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ref_ku 
                        ON rc.UNIQUE_CONSTRAINT_NAME = ref_ku.CONSTRAINT_NAME
                ) fk ON c.TABLE_SCHEMA = fk.TABLE_SCHEMA 
                     AND c.TABLE_NAME = fk.TABLE_NAME 
                     AND c.COLUMN_NAME = fk.COLUMN_NAME
                WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
                ORDER BY c.ORDINAL_POSITION";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@schema", schema);
            command.Parameters.AddWithValue("@table", table);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            if (!reader.HasRows)
            {
                return $"Table '{schema}.{table}' not found.";
            }
            
            return await FormatQueryResults(reader, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Error describing table: {ex.Message}";
        }
    }

    [McpServerTool, Description("List all schemas (databases) available in the SQL Server instance.")]
    public async Task<string> ListSchemas(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            
            var query = @"
                SELECT 
                    SCHEMA_NAME,
                    SCHEMA_OWNER,
                    DEFAULT_CHARACTER_SET_CATALOG,
                    DEFAULT_CHARACTER_SET_SCHEMA,
                    DEFAULT_CHARACTER_SET_NAME
                FROM INFORMATION_SCHEMA.SCHEMATA
                ORDER BY SCHEMA_NAME";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            return await FormatQueryResults(reader, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Error listing schemas: {ex.Message}";
        }
    }

    private static async Task<string> FormatQueryResults(SqlDataReader reader, CancellationToken cancellationToken)
    {
        var result = new System.Text.StringBuilder();
        
        if (!reader.HasRows)
        {
            return "Query executed successfully. No rows returned.";
        }

        // Get column headers
        var columnCount = reader.FieldCount;
        var columnNames = new string[columnCount];
        var columnWidths = new int[columnCount];
        
        for (int i = 0; i < columnCount; i++)
        {
            columnNames[i] = reader.GetName(i);
            columnWidths[i] = Math.Max(columnNames[i].Length, 10); // Minimum width of 10
        }

        // Read all rows to determine column widths
        var rows = new List<object[]>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new object[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                row[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i);
                var valueLength = row[i]?.ToString()?.Length ?? 4;
                columnWidths[i] = Math.Max(columnWidths[i], valueLength);
            }
            rows.Add(row);
        }

        // Build header
        result.AppendLine(string.Join(" | ", columnNames.Select((name, i) => name.PadRight(columnWidths[i]))));
        result.AppendLine(string.Join("-+-", columnWidths.Select(w => new string('-', w))));

        // Build data rows
        foreach (var row in rows)
        {
            result.AppendLine(string.Join(" | ", row.Select((value, i) => 
                (value?.ToString() ?? "NULL").PadRight(columnWidths[i]))));
        }

        result.AppendLine($"\n({rows.Count} row(s) returned)");
        
        return result.ToString();
    }
} 