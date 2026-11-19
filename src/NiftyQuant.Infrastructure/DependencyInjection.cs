using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Infrastructure.Persistence;
using NiftyQuant.Infrastructure.Services;
using NiftyQuant.Infrastructure.WebSockets;

namespace NiftyQuant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind Options
        services.Configure<UpstoxOptions>(configuration.GetSection(UpstoxOptions.SectionName));
        services.Configure<RiskOptions>(configuration.GetSection(RiskOptions.SectionName));

        // Configure SQL Server DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=.\\SQLSERVER2022_2;Database=NiftyQuantDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

        services.AddDbContext<NiftyQuantDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(NiftyQuantDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        });

        // Upstox HTTP Client with Authentication Handler and Resilience
        services.AddTransient<UpstoxAuthenticationHandler>();

        services.AddHttpClient<IUpstoxClient, UpstoxClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<UpstoxOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<UpstoxAuthenticationHandler>()
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
        });

        // Auth client used by WebSocket streamer for dynamic authorize redirects
        services.AddHttpClient("UpstoxAuthClient", (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<UpstoxOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<UpstoxAuthenticationHandler>();

        // Singleton WebSocket Streamer and Subscription Manager
        services.AddSingleton<IUpstoxWebSocketClient, UpstoxWebSocketClient>();
        services.AddSingleton<ISubscriptionManager, SubscriptionManager>();

        return services;
    }
}
