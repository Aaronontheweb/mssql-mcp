using Akka.Hosting;
using Microsoft.Extensions.Hosting;
using MSSQL.MCP.Configuration;
using MSSQL.MCP.Database;

var hostBuilder = new HostBuilder();

hostBuilder
    .ConfigureAppConfiguration((context, builder) =>
    {
        builder.AddEnvironmentVariables();
        // Map MSSQL_CONNECTION_STRING to Database:ConnectionString
        builder.AddInMemoryCollection([
            new KeyValuePair<string, string?>("Database:ConnectionString", 
                Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING"))
        ]);
    })
    .ConfigureServices((context, services) =>
{
    // Configure Database options with validation
    services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
    services.AddOptionsWithValidateOnStart<DatabaseOptions>()
        .BindConfiguration("Database");

    // Register SQL Connection Factory
    services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

    services.AddAkka("MyActorSystem", (builder, sp) =>
    {
        
    });
});

var host = hostBuilder.Build();

// Validate database connection on startup
var connectionFactory = host.Services.GetRequiredService<ISqlConnectionFactory>();
var isConnectionValid = await connectionFactory.ValidateConnectionAsync();
if (!isConnectionValid)
{
    throw new InvalidOperationException("Unable to connect to the database with the provided connection string. Please verify MSSQL_CONNECTION_STRING is correct and the database is accessible.");
}

Console.WriteLine("✅ Database connection validated successfully");

await host.RunAsync();