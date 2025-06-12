using MSSQL.MCP.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MSSQL.MCP.IntegrationTests.Configuration;

/// <summary>
/// Tests for DatabaseOptions configuration validation.
/// </summary>
public class DatabaseOptionsTests
{
    [Fact]
    public void DatabaseOptionsValidator_WithValidConnectionString_ReturnsSuccess()
    {
        // Arrange
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            ConnectionString = "Server=localhost;Database=Test;Trusted_Connection=true;"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void DatabaseOptionsValidator_WithEmptyConnectionString_ReturnsFail()
    {
        // Arrange
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            ConnectionString = ""
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("MSSQL_CONNECTION_STRING", result.FailureMessage);
    }

    [Fact]
    public void DatabaseOptionsValidator_WithNullConnectionString_ReturnsFail()
    {
        // Arrange
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            ConnectionString = null!
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("MSSQL_CONNECTION_STRING", result.FailureMessage);
    }

    [Fact]
    public void DatabaseOptionsValidator_WithWhitespaceConnectionString_ReturnsFail()
    {
        // Arrange
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            ConnectionString = "   "
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("MSSQL_CONNECTION_STRING", result.FailureMessage);
    }

    [Fact]
    public void DatabaseOptions_SectionName_IsCorrect()
    {
        // Assert
        Assert.Equal("Database", DatabaseOptions.SectionName);
    }

    [Fact]
    public void DatabaseOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new DatabaseOptions();

        // Assert
        Assert.Equal(string.Empty, options.ConnectionString);
    }

    [Fact]
    public void DatabaseOptions_Integration_WithDependencyInjection()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Simulate configuration
        var connectionString = "Server=localhost;Database=Test;Trusted_Connection=true;";
        
        services.Configure<DatabaseOptions>(options =>
        {
            options.ConnectionString = connectionString;
        });
        
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal(connectionString, options.Value.ConnectionString);
    }
} 