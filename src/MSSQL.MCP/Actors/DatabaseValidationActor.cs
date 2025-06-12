using MSSQL.MCP.Database;

namespace MSSQL.MCP.Actors;

/// <summary>
/// Validates the database connection at startup, so that the MCP server can only start if the database is accessible.
/// </summary>
public sealed class DatabaseValidationActor : ReceiveActor
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILoggingAdapter _logger;

    public DatabaseValidationActor(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        _logger = Context.GetLogger();
        
        ReceiveAsync<ValidateDatabaseConnection>(_ => Handle());
    }

    protected override void PreStart()
    {
        _logger.Info("🔌 Starting database connection validation...");
        Self.Tell(new ValidateDatabaseConnection());
    }

    private async Task Handle()
    {
        try
        {
            var isValid = await _connectionFactory.ValidateConnectionAsync();

            if (isValid)
            {
                _logger.Info("✅ Database connection validated successfully! Ready to process MCP requests.");
            }
            else
            {
                _logger.Error("❌ Database connection validation failed: Unable to connect to the database");
                _logger.Error("💡 Please verify that:");
                _logger.Error("   • MSSQL_CONNECTION_STRING environment variable is set correctly");
                _logger.Error("   • SQL Server instance is running and accessible");
                _logger.Error("   • Database exists and credentials are valid");
                _logger.Error("   • Network connectivity to the database server");
                _logger.Error("🛑 Shutting down MCP server due to database connection failure");

                _ = Context.System.Terminate();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "❌ Database connection validation failed with exception: {Message}", ex.Message);
            _logger.Error("💡 Connection string format should be similar to:");
            _logger.Error("   Server=localhost;Database=MyDatabase;Trusted_Connection=true;");
            _logger.Error("   or");
            _logger.Error("   Server=localhost;Database=MyDatabase;User Id=myuser;Password=mypassword;");
            _logger.Error("🛑 Shutting down MCP server due to database connection error");

            _ = Context.System.Terminate();
        }
        finally
        {
            // shut ourselves down once finished
            Context.Stop(Self);
        }
    }
}

public record ValidateDatabaseConnection; 