using Akka.Hosting;
using Akka.Console;
using Microsoft.Extensions.Hosting;
using MSSQL.MCP.Configuration;

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

    services.AddAkka("MyActorSystem", (builder, sp) =>
    {
        builder
            .WithActors((system, registry, resolver) =>
            {
                var helloActor = system.ActorOf(Props.Create(() => new HelloActor()), "hello-actor");
                registry.Register<HelloActor>(helloActor);
            })
            .WithActors((system, registry, resolver) =>
            {
                var timerActorProps =
                    resolver.Props<TimerActor>(); // uses Msft.Ext.DI to inject reference to helloActor
                var timerActor = system.ActorOf(timerActorProps, "timer-actor");
                registry.Register<TimerActor>(timerActor);
            });
    });
});

var host = hostBuilder.Build();

await host.RunAsync();