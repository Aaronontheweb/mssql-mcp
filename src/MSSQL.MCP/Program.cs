using Akka.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MSSQL.MCP.Configuration;
using MSSQL.MCP.Database;
using MSSQL.MCP.Actors;

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
    // Configure logging to stderr for MCP protocol compatibility
    services.AddLogging(builder =>
    {
        builder.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Trace;
        });
    });

    // Configure Database options with validation
    services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
    services.AddOptionsWithValidateOnStart<DatabaseOptions>()
        .BindConfiguration("Database");

    // Register SQL Connection Factory
    services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

    // Add MCP Server
    services.AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    // Add Akka.NET
    services.AddAkka("MSSQLMcpActorSystem", (builder, sp) =>
    {
        builder
            .ConfigureLoggers(configBuilder =>
            {
                configBuilder.ClearLoggers();
                configBuilder.AddLoggerFactory();
            })
            .WithActors((system, registry, resolver) =>
            {
                // Database validation actor - tests actual connection
                var dbValidationActorProps = resolver.Props<DatabaseValidationActor>();
                var dbValidationActor = system.ActorOf(dbValidationActorProps, "database-validation");
                
                // We would normally register this actor in the registry, but since it dies immediately after validation,
                // there's not much point in keeping it around.
            });
    });
});

var host = hostBuilder.Build();

await host.RunAsync();