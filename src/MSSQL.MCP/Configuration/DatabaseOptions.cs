using System.ComponentModel.DataAnnotations;

namespace MSSQL.MCP.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(ErrorMessage = "MSSQL_CONNECTION_STRING environment variable is required")]
    public string ConnectionString { get; set; } = string.Empty;
}

public class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("MSSQL_CONNECTION_STRING environment variable must be provided and cannot be empty");
        }

        return ValidateOptionsResult.Success;
    }
} 