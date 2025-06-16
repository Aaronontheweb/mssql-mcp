using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MSSQL.MCP.Database;

namespace MSSQL.MCP.Tools;

[McpServerToolType]
public class SqlExecutionTool(ISqlConnectionFactory connectionFactory, ILogger<SqlExecutionTool> logger)
{
    // Regex to detect valid T-SQL keywords at the beginning of queries
    private static readonly Regex ValidTSqlStartPattern = new(
        @"^\s*(SELECT|INSERT|UPDATE|DELETE|WITH|CREATE|ALTER|DROP|GRANT|REVOKE|EXEC|EXECUTE|DECLARE|SET|USE|BACKUP|RESTORE|TRUNCATE|MERGE)\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [McpServerTool, Description(@"Execute T-SQL queries against the connected Microsoft SQL Server database. 
    
IMPORTANT: This tool ONLY accepts valid T-SQL (Transact-SQL) syntax for Microsoft SQL Server.

Supported operations:
- SELECT statements for data retrieval
- INSERT, UPDATE, DELETE for data modification  
- CREATE, ALTER, DROP for schema changes
- WITH clauses for CTEs (Common Table Expressions)
- EXEC/EXECUTE for stored procedures
- And other valid T-SQL statements

Examples of valid T-SQL:
- SELECT * FROM Users WHERE Active = 1
- INSERT INTO Products (Name, Price) VALUES ('Widget', 19.99)
- UPDATE Customers SET Status = 'Active' WHERE ID = 123
- CREATE TABLE Orders (ID int PRIMARY KEY, CustomerID int)

The query parameter must contain ONLY the T-SQL statement - no explanations, markdown, or other text.")]
    public async Task<string> ExecuteSql(
        [Description(@"The T-SQL query to execute. Must be valid Microsoft SQL Server T-SQL syntax only. 
        Examples: 'SELECT * FROM Users', 'INSERT INTO Products VALUES (1, ''Name'')', 'CREATE TABLE Test (ID int)'
        Do NOT include explanations, markdown formatting, or non-SQL text.")] 
        string query,
        CancellationToken cancellationToken = default)
    {
        // Log the incoming query for debugging
        logger.LogInformation("Received SQL execution request. Query length: {QueryLength} characters", query.Length );
        logger.LogDebug("SQL Query received: {Query}", query);

        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogWarning("Empty or null query received");
            return "Error: SQL query cannot be empty";
        }

        // Validate that the query looks like T-SQL
        var trimmedQuery = query.Trim();
        if (!ValidTSqlStartPattern.IsMatch(trimmedQuery))
        {
            logger.LogWarning("Invalid T-SQL query received. Query does not start with valid T-SQL keyword: {QueryStart}", 
                trimmedQuery.Length > 50 ? trimmedQuery[..50] + "..." : trimmedQuery);
            
            return @"Error: Invalid T-SQL syntax. This tool only accepts valid Microsoft SQL Server T-SQL statements.

Valid T-SQL statements must start with keywords like:
- SELECT (for data retrieval)
- INSERT, UPDATE, DELETE (for data modification)  
- CREATE, ALTER, DROP (for schema changes)
- WITH (for CTEs)
- EXEC/EXECUTE (for stored procedures)
- And other valid T-SQL keywords

Examples:
✓ SELECT * FROM Users
✓ INSERT INTO Products (Name) VALUES ('Test')
✓ CREATE TABLE Orders (ID int)

✗ Please show me all users
✗ Can you create a table for orders?
✗ ```sql SELECT * FROM Users```

Please provide only the T-SQL statement without explanations or formatting.";
        }

        try
        {
            logger.LogInformation("Executing T-SQL query starting with: {QueryStart}", 
                trimmedQuery.Length > 30 ? trimmedQuery[..30] + "..." : trimmedQuery);

            await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand(query, connection);
            
            // Determine if this is a SELECT query or a command
            var isSelectQuery = trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                               trimmedQuery.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);

            if (isSelectQuery)
            {
                // Handle SELECT queries - return data
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var result = await FormatQueryResults(reader, cancellationToken);
                logger.LogInformation("SELECT query executed successfully");
                return result;
            }
            else
            {
                // Handle INSERT/UPDATE/DELETE/DDL - return affected rows
                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                var result = $"Query executed successfully. Rows affected: {rowsAffected}";
                logger.LogInformation("Non-SELECT query executed successfully. Rows affected: {RowsAffected}", rowsAffected);
                return result;
            }
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "SQL execution failed with SQL error: {SqlErrorMessage}", ex.Message);
            return $"SQL Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SQL execution failed with general error: {ErrorMessage}", ex.Message);
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool, Description("List all tables in the database with basic information.")]
    public async Task<string> ListTables(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            
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

            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            return await FormatQueryResults(reader, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Error listing tables: {ex.Message}";
        }
    }

    [McpServerTool, Description("List all schemas (databases) available in the SQL Server instance.")]
    public async Task<string> ListSchemas(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            
            var query = @"
                SELECT 
                    SCHEMA_NAME,
                    SCHEMA_OWNER,
                    DEFAULT_CHARACTER_SET_CATALOG,
                    DEFAULT_CHARACTER_SET_SCHEMA,
                    DEFAULT_CHARACTER_SET_NAME
                FROM INFORMATION_SCHEMA.SCHEMATA
                ORDER BY SCHEMA_NAME";
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
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
                var valueLength = row[i].ToString()?.Length ?? 4;
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
                (value.ToString() ?? "NULL").PadRight(columnWidths[i]))));
        }

        result.AppendLine($"\n({rows.Count} row(s) returned)");
        
        return result.ToString();
    }
} 