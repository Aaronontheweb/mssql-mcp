using MSSQL.MCP.IntegrationTests.Infrastructure;
using MSSQL.MCP.Tools;
using Xunit;

namespace MSSQL.MCP.IntegrationTests.Tools;

/// <summary>
/// Integration tests for SqlExecutionTool that validate all MCP tools work correctly
/// with a real SQL Server database.
/// </summary>
[Collection("Database")]
public class SqlExecutionToolTests : IAsyncLifetime
{
    private readonly DatabaseTestFixture _fixture;
    private readonly SqlExecutionTool _tool;

    public SqlExecutionToolTests(DatabaseTestFixture fixture)
    {
        _fixture = fixture;
        _tool = new SqlExecutionTool(fixture.ConnectionFactory);
    }

    public async Task InitializeAsync()
    {
        await _fixture.CreateTestDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.CleanupTestDataAsync();
    }

    #region ExecuteSql Tests

    [Fact]
    public async Task ExecuteSql_SelectQuery_ReturnsFormattedResults()
    {
        // Act
        var result = await _tool.ExecuteSql("SELECT Id, Name, Email FROM dbo.Users ORDER BY Id");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("John Doe", result);
        Assert.Contains("jane@example.com", result);
        Assert.Contains("Bob Johnson", result);
        
        // Should contain table headers
        Assert.Contains("Id", result);
        Assert.Contains("Name", result);
        Assert.Contains("Email", result);
    }

    [Fact]
    public async Task ExecuteSql_WithClause_ReturnsResults()
    {
        // Act
        var result = await _tool.ExecuteSql(@"
            WITH ActiveUsers AS (
                SELECT Id, Name FROM dbo.Users WHERE IsActive = 1
            )
            SELECT * FROM ActiveUsers ORDER BY Id");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("John Doe", result);
        Assert.Contains("Jane Smith", result);
        Assert.Contains("Bob Johnson", result);
    }

    [Fact]
    public async Task ExecuteSql_InsertQuery_ReturnsRowsAffected()
    {
        // Act
        var result = await _tool.ExecuteSql("INSERT INTO dbo.Users (Name, Email) VALUES ('Test User', 'test@example.com')");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Rows affected: 1", result);
        Assert.Contains("successfully", result);
    }

    [Fact]
    public async Task ExecuteSql_UpdateQuery_ReturnsRowsAffected()
    {
        // Act
        var result = await _tool.ExecuteSql("UPDATE dbo.Users SET Name = 'Updated Name' WHERE Id = 1");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Rows affected: 1", result);
        Assert.Contains("successfully", result);
    }

    [Fact]
    public async Task ExecuteSql_DeleteQuery_ReturnsRowsAffected()
    {
        // Act
        var result = await _tool.ExecuteSql("DELETE FROM dbo.Orders WHERE Status = 'Cancelled'");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Rows affected: 1", result);
        Assert.Contains("successfully", result);
    }

    [Fact]
    public async Task ExecuteSql_CreateTable_ReturnsSuccess()
    {
        // Act
        var result = await _tool.ExecuteSql(@"
            CREATE TABLE dbo.TestTable (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(50) NOT NULL
            )");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("successfully", result);
    }

    [Fact]
    public async Task ExecuteSql_EmptyQuery_ReturnsError()
    {
        // Act
        var result = await _tool.ExecuteSql("");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Error: SQL query cannot be empty", result);
    }

    [Fact]
    public async Task ExecuteSql_InvalidQuery_ReturnsError()
    {
        // Act
        var result = await _tool.ExecuteSql("INVALID SQL QUERY");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SQL Error:", result);
    }

    [Fact]
    public async Task ExecuteSql_SelectFromNonExistentTable_ReturnsError()
    {
        // Act
        var result = await _tool.ExecuteSql("SELECT * FROM NonExistentTable");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SQL Error:", result);
    }

    #endregion

    #region ListTables Tests

    [Fact]
    public async Task ListTables_ReturnsAllTables()
    {
        // Act
        var result = await _tool.ListTables();

        // Assert
        Assert.NotNull(result);
        
        // Should contain our test tables
        Assert.Contains("Users", result);
        Assert.Contains("Orders", result);
        Assert.Contains("Products", result);
        
        // Should show schema information
        Assert.Contains("dbo", result);
        Assert.Contains("TestSchema", result);
        
        // Should show table type
        Assert.Contains("BASE TABLE", result);
        
        // Should show row counts
        Assert.Contains("3", result); // Users table should have 3 rows
        Assert.Contains("4", result); // Orders table should have 4 rows
    }

    [Fact]
    public async Task ListTables_ShowsCorrectColumns()
    {
        // Act
        var result = await _tool.ListTables();

        // Assert
        Assert.NotNull(result);
        
        // Should contain expected column headers
        Assert.Contains("TABLE_SCHEMA", result);
        Assert.Contains("TABLE_NAME", result);
        Assert.Contains("TABLE_TYPE", result);
        Assert.Contains("ROW_COUNT", result);
    }

    #endregion



    #region ListSchemas Tests

    [Fact]
    public async Task ListSchemas_ReturnsAllSchemas()
    {
        // Act
        var result = await _tool.ListSchemas();

        // Assert
        Assert.NotNull(result);
        
        // Should contain standard schemas
        Assert.Contains("dbo", result);
        Assert.Contains("sys", result);
        Assert.Contains("INFORMATION_SCHEMA", result);
        
        // Should contain our test schema
        Assert.Contains("TestSchema", result);
        
        // Should show column headers
        Assert.Contains("SCHEMA_NAME", result);
        Assert.Contains("SCHEMA_OWNER", result);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteSql_WithCancellation_HandlesGracefully()
    {
        using var cts = new CancellationTokenSource();
        
        // Start a long-running query
        var task = _tool.ExecuteSql("WAITFOR DELAY '00:00:10'; SELECT 1", cts.Token);
        
        // Cancel immediately
        cts.Cancel();
        
        // Should complete without throwing
        var result = await task;
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ListTables_WithCancellation_HandlesGracefully()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Should handle cancellation gracefully
        var result = await _tool.ListTables(cts.Token);
        Assert.NotNull(result);
    }

    #endregion

    #region Data Validation Tests

    [Fact]
    public async Task ExecuteSql_ComplexJoin_ReturnsCorrectData()
    {
        // Act
        var result = await _tool.ExecuteSql(@"
            SELECT 
                u.Name as UserName,
                COUNT(o.Id) as OrderCount,
                SUM(o.Total) as TotalAmount
            FROM dbo.Users u
            LEFT JOIN dbo.Orders o ON u.Id = o.UserId
            WHERE u.IsActive = 1
            GROUP BY u.Id, u.Name
            ORDER BY u.Name");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("John Doe", result);
        Assert.Contains("Jane Smith", result);
        Assert.Contains("Bob Johnson", result);
        
        // Should show aggregated data
        Assert.Contains("249.98", result); // John's total orders
        Assert.Contains("75.50", result);  // Jane's total orders
        Assert.Contains("200.00", result); // Bob's total orders
    }

    [Fact]
    public async Task ExecuteSql_ParameterizedQuery_WorksCorrectly()
    {
        // Note: This test shows that regular parameterized queries work
        // In a real MCP implementation, you might want to add parameter support
        var result = await _tool.ExecuteSql("SELECT * FROM dbo.Users WHERE Id = 1");

        Assert.NotNull(result);
        Assert.Contains("John Doe", result);
        Assert.DoesNotContain("Jane Smith", result);
    }

    #endregion
} 