using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using NiftyQuant.Api.Hubs;
using NiftyQuant.Api.Services;
using NiftyQuant.Application;
using NiftyQuant.Infrastructure;
using NiftyQuant.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Swagger Documentation
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NiftyQuant Quantitative Trading API",
        Version = "v1",
        Description = "NIFTY 50 Options Quantitative Trading System REST API with Multi-Factor Scoring, Risk Engine, and Paper Trading.",
        Contact = new OpenApiContact
        {
            Name = "NiftyQuant Team"
        }
    });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
              {
                  if (string.IsNullOrEmpty(origin)) return false;
                  var uri = new Uri(origin);
                  return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                         uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register Infrastructure (SQL Server, EF Core, Options)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Real-Time SignalR Bridge Service
builder.Services.AddHostedService<SignalRBridgeService>();

// Configure Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NiftyQuantDbContext>(
        name: "sqlserver",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "sql", "ready" });

var app = builder.Build();

// Ensure Database is Created on Startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
        logger.LogInformation("Verifying and applying database schema...");
        db.Database.EnsureCreated();
        logger.LogInformation("Database is ready and verified.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

app.UseSerilogRequestLogging();
app.UseCors("AllowLocalhost");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Swagger JSON & Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NiftyQuant API v1");
    c.RoutePrefix = "swagger";
});

// Modern Scalar API reference UI at /scalar/v1
app.MapScalarApiReference(options =>
{
    options.WithOpenApiRoutePattern("/swagger/v1/swagger.json");
    options.WithTitle("NiftyQuant Trading Engine API");
});

// Health Check Endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                error = e.Value.Exception?.Message
            })
        }, new JsonSerializerOptions { WriteIndented = true });
        await context.Response.WriteAsync(result);
    }
});

app.MapHub<MarketHub>("/hubs/market");
app.MapControllers();

// Root route redirect directly to Swagger UI
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
