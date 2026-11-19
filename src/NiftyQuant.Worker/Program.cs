using NiftyQuant.Infrastructure;
using NiftyQuant.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Register Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Register Hosted Services
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
