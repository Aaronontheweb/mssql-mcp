using Xunit;

namespace MSSQL.MCP.IntegrationTests.Infrastructure;

/// <summary>
/// Test collection definition that ensures all integration tests share the same database fixture.
/// This improves performance by reusing the SQL Server container across tests.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseTestCollection : ICollectionFixture<DatabaseTestFixture>
{
    // This class has no code, it exists solely to define the collection
} 