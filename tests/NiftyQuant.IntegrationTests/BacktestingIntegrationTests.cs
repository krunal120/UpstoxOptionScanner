using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Backtesting;
using NiftyQuant.Infrastructure;
using Xunit;

namespace NiftyQuant.IntegrationTests;

public class BacktestingIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBacktestingEngine _backtestEngine;

    public BacktestingIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets("niftyquant-api-secrets-2026")
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _backtestEngine = _serviceProvider.GetRequiredService<IBacktestingEngine>();
    }

    [Fact]
    public async Task RunBacktestAsync_WithLiveHistoricalCandles_ExecutesMultiDaySimulation()
    {
        // Arrange: Last 5 trading days
        var now = DateTime.Now;
        var request = new BacktestRequest
        {
            UnderlyingKey = "NSE_INDEX|Nifty 50",
            FromDate = now.AddDays(-7),
            ToDate = now,
            InitialCapital = 50000m,
            RiskPerTradePercent = 0.5m,
            MaxDailyLossPercent = 1.5m,
            MaxDailyTrades = 6,
            MaxConsecutiveLosses = 2,
            SimulateSlippage = true,
            SlippagePercent = 0.2m,
            EnableTrailingStop = true
        };

        // Act
        var result = await _backtestEngine.RunBacktestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.UnderlyingKey.Should().Be("NSE_INDEX|Nifty 50");
        result.InitialCapital.Should().Be(50000m);
        result.TotalTradingDays.Should().BeGreaterThan(0);
        result.EquityCurve.Should().NotBeEmpty();
        result.DailyPerformance.Should().NotBeEmpty();
        result.MaxDrawdownPercent.Should().BeGreaterThanOrEqualTo(0m);
    }
}
