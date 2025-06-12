using MSSQL.MCP.Database;
using MSSQL.MCP.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace MSSQL.MCP.IntegrationTests.Database;

/// <summary>
/// Integration tests for SqlConnectionFactory to validate connection handling.
/// </summary>
[Collection("Database")]
public class SqlConnectionFactoryTests
{
    private readonly DatabaseTestFixture _fixture;

    public SqlConnectionFactoryTests(DatabaseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void CreateConnection_ReturnsValidConnection()
    {
        // Act
        using var connection = _fixture.ConnectionFactory.CreateConnection();

        // Assert
        Assert.NotNull(connection);
        Assert.IsType<SqlConnection>(connection);
        Assert.Equal(_fixture.ConnectionString, connection.ConnectionString);
    }

    [Fact]
    public async Task CreateOpenConnectionAsync_ReturnsOpenConnection()
    {
        // Act
        using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task CreateOpenConnectionAsync_WithCancellation_HandlesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should handle cancellation token (TaskCanceledException inherits from OperationCanceledException)
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync(cts.Token);
        });
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidConnection_ReturnsTrue()
    {
        // Act
        var isValid = await _fixture.ConnectionFactory.ValidateConnectionAsync();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithTimeout_CompletesQuickly()
    {
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var isValid = await _fixture.ConnectionFactory.ValidateConnectionAsync();
        stopwatch.Stop();

        // Assert
        Assert.True(isValid);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000); // Should complete within 5 seconds
    }

    [Fact]
    public async Task CreateOpenConnectionAsync_MultipleConnections_WorkCorrectly()
    {
        // Act & Assert - Test connection pooling
        var tasks = new List<Task>();
        
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
                using var command = new SqlCommand("SELECT 1", connection);
                var result = await command.ExecuteScalarAsync();
                Assert.Equal(1, result);
            }));
        }

        await Task.WhenAll(tasks);
    }
} 