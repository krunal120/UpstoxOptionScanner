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
        services.Configure<MegaBullOptions>(configuration.GetSection(MegaBullOptions.SectionName));

        // Configure Database DbContext (Dual Provider: SQLite for Cloud/Container or SQL Server for Local)
        var databaseProvider = configuration["DatabaseProvider"] ?? "SqlServer";
        var rawConnStr = configuration.GetConnectionString("DefaultConnection");

        var isSqlite = databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ||
                       (!string.IsNullOrEmpty(rawConnStr) && (
                           rawConnStr.Contains(".db", StringComparison.OrdinalIgnoreCase) ||
                           (rawConnStr.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) && 
                            !rawConnStr.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase) && 
                            !rawConnStr.Contains("Database=", StringComparison.OrdinalIgnoreCase))
                       ));

        string connectionString;
        if (isSqlite)
        {
            connectionString = (!string.IsNullOrEmpty(rawConnStr) && (rawConnStr.Contains(".db", StringComparison.OrdinalIgnoreCase) || rawConnStr.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)))
                ? rawConnStr
                : "Data Source=niftyquant.db";
        }
        else
        {
            connectionString = rawConnStr 
                ?? "Server=.\\SQLSERVER2022_2;Database=NiftyQuantDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";
        }

        services.AddDbContext<NiftyQuantDbContext>(options =>
        {
            if (isSqlite)
            {
                options.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(typeof(NiftyQuantDbContext).Assembly.FullName);
                });
            }
            else
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(NiftyQuantDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
            }
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

        // Storage & Snapshot Engines
        services.AddSingleton<ICandleAggregationEngine, CandleAggregationEngine>();
        services.AddSingleton<IOptionChainSnapshotEngine, OptionChainSnapshotEngine>();

        // Strategy Engine
        services.AddSingleton<IStrategyEngine, StrategyEngine>();

        // Risk Management Engine
        services.AddSingleton<IRiskManagementService, RiskManagementService>();

        // Paper Trading Execution Engine
        services.AddSingleton<IPaperTradingEngine, PaperTradingEngine>();

        // Autonomous Trading Execution Service
        services.AddSingleton<AutonomousTradingService>();
        services.AddSingleton<IAutonomousTradingService>(sp => sp.GetRequiredService<AutonomousTradingService>());

        // Upstox Market Feed WebSocket Background Service
        services.AddSingleton<MarketFeedService>();

        // Backtesting Engine
        services.AddSingleton<IBacktestingEngine, BacktestingEngine>();

        // MegaBull External Paper Trading Service
        services.AddHttpClient<IMegaBullService, MegaBullService>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MegaBullOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
