using NiftyQuant.Application;
using NiftyQuant.Infrastructure;
using NiftyQuant.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog Logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Services.AddSerilog(Log.Logger);

// Register Application & Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Register Autonomous Quantitative Worker
builder.Services.AddHostedService<Worker>();

try
{
    Log.Information("Starting NiftyQuant Autonomous Worker service...");
    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NiftyQuant Autonomous Worker terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
