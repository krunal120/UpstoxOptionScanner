using NiftyQuant.Application;
using NiftyQuant.Infrastructure;
using NiftyQuant.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Register Application & Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Register Hosted Services
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
