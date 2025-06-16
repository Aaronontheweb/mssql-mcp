using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MSSQL.MCP.Configuration;
using MSSQL.MCP.Database;
using Testcontainers.MsSql;
using Xunit;

namespace MSSQL.MCP.IntegrationTests.Infrastructure;

/// <summary>
/// Test fixture that provides a SQL Server container for integration tests.
/// This fixture is shared across all tests in the collection to improve performance.
/// </summary>
public class DatabaseTestFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    
    public string ConnectionString { get; private set; } = string.Empty;
    public ISqlConnectionFactory ConnectionFactory { get; private set; } = null!;
    
    public async Task InitializeAsync()
    {
        // Start SQL Server container
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Test123!")
            .WithCleanUp(true)
            .Build();
            
        await _container.StartAsync();
        
        ConnectionString = _container.GetConnectionString();
        
        // Create connection factory for tests
        var services = new ServiceCollection();
        services.Configure<DatabaseOptions>(options =>
        {
            options.ConnectionString = ConnectionString;
        });
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddLogging(builder => builder.AddConsole());
        
        var serviceProvider = services.BuildServiceProvider();
        ConnectionFactory = serviceProvider.GetRequiredService<ISqlConnectionFactory>();
        
        // Verify connection works
        var isValid = await ConnectionFactory.ValidateConnectionAsync();
        if (!isValid)
        {
            throw new InvalidOperationException("Failed to establish connection to test SQL Server container");
        }
    }
    
    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
    
    /// <summary>
    /// Creates a test database with sample schema and data
    /// </summary>
    public async Task CreateTestDatabaseAsync()
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync();
        
        // Create test schema and tables
        var setupSql = @"
            -- Create test schema
            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'TestSchema')
                EXEC('CREATE SCHEMA TestSchema');
            
            -- Create Users table
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users' AND schema_id = SCHEMA_ID('dbo'))
            BEGIN
                CREATE TABLE dbo.Users (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    Email NVARCHAR(255) UNIQUE NOT NULL,
                    CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
                    IsActive BIT DEFAULT 1
                );
            END;
            
            -- Create Orders table with foreign key
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders' AND schema_id = SCHEMA_ID('dbo'))
            BEGIN
                CREATE TABLE dbo.Orders (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    UserId INT NOT NULL,
                    OrderDate DATETIME2 DEFAULT GETUTCDATE(),
                    Total DECIMAL(10,2) NOT NULL,
                    Status NVARCHAR(50) DEFAULT 'Pending',
                    FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
                );
            END;
            
            -- Create table in test schema
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products' AND schema_id = SCHEMA_ID('TestSchema'))
            BEGIN
                CREATE TABLE TestSchema.Products (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    Price DECIMAL(10,2) NOT NULL,
                    Category NVARCHAR(50),
                    InStock BIT DEFAULT 1
                );
            END;
            
            -- Insert sample data
            IF NOT EXISTS (SELECT * FROM dbo.Users)
            BEGIN
                INSERT INTO dbo.Users (Name, Email) VALUES
                    ('John Doe', 'john@example.com'),
                    ('Jane Smith', 'jane@example.com'),
                    ('Bob Johnson', 'bob@example.com');
                    
                INSERT INTO dbo.Orders (UserId, Total, Status) VALUES
                    (1, 99.99, 'Completed'),
                    (1, 149.99, 'Pending'),
                    (2, 75.50, 'Completed'),
                    (3, 200.00, 'Cancelled');
                    
                INSERT INTO TestSchema.Products (Name, Price, Category) VALUES
                    ('Laptop', 999.99, 'Electronics'),
                    ('Mouse', 29.99, 'Electronics'),
                    ('Book', 15.99, 'Education');
            END;
        ";
        
        await using var command = new Microsoft.Data.SqlClient.SqlCommand(setupSql, connection);
        await command.ExecuteNonQueryAsync();
    }
    
    /// <summary>
    /// Cleans up test data between tests
    /// </summary>
    public async Task CleanupTestDataAsync()
    {
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync();
        
        var cleanupSql = @"
            DELETE FROM dbo.Orders;
            DELETE FROM dbo.Users;
            DELETE FROM TestSchema.Products;
            
            -- Reset identity seeds
            DBCC CHECKIDENT ('dbo.Orders', RESEED, 0);
            DBCC CHECKIDENT ('dbo.Users', RESEED, 0);
            DBCC CHECKIDENT ('TestSchema.Products', RESEED, 0);
        ";
        
        await using var command = new Microsoft.Data.SqlClient.SqlCommand(cleanupSql, connection);
        await command.ExecuteNonQueryAsync();
    }
} 