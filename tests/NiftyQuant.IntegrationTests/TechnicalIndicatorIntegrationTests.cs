using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure;
using Xunit;

namespace NiftyQuant.IntegrationTests;

public class TechnicalIndicatorIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly IUpstoxClient _upstoxClient;

    public TechnicalIndicatorIntegrationTests()
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
        _indicatorService = _serviceProvider.GetRequiredService<ITechnicalIndicatorService>();
        _upstoxClient = _serviceProvider.GetRequiredService<IUpstoxClient>();
    }

    [Fact]
    public async Task CalculateIndicators_WithLiveUpstoxCandles_ReturnsValidSnapshot()
    {
        // Arrange: Fetch real 1-minute historical candles from Upstox for NIFTY 50
        const string key = "NSE_INDEX|Nifty 50";
        var now = DateTime.Now;
        var toDate = now.ToString("yyyy-MM-dd");
        var fromDate = now.AddDays(-3).ToString("yyyy-MM-dd");

        var rawCandles = await _upstoxClient.GetHistoricalCandlesAsync(key, "minutes", 1, toDate, fromDate);
        rawCandles.Should().NotBeEmpty();

        var candles = rawCandles.Select(c => new MarketCandle
        {
            InstrumentKey = key,
            Timeframe = CandleTimeframe.OneMinute,
            CandleTimeUtc = c.Timestamp,
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            Volume = c.Volume,
            OpenInterest = c.OpenInterest
        }).ToList();

        // Act: Compute all technical indicators
        var snapshot = _indicatorService.CalculateIndicators(candles, CandleTimeframe.OneMinute);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.ClosePrice.Should().BeGreaterThan(15000m);
        snapshot.Ema9.Should().NotBeNull();
        snapshot.Ema21.Should().NotBeNull();
        snapshot.Vwap.Should().NotBeNull();
        snapshot.Supertrend.Should().NotBeNull();
        snapshot.Rsi14.Should().NotBeNull();
        snapshot.Rsi14!.Value.Should().BeInRange(0m, 100m);
        snapshot.Macd.Should().NotBeNull();
        snapshot.Adx14.Should().NotBeNull();
        snapshot.Atr14.Should().NotBeNull();
        snapshot.BollingerMiddle.Should().NotBeNull();
        snapshot.BollingerUpper.Should().BeGreaterThan(snapshot.BollingerMiddle!.Value);
        snapshot.BollingerLower.Should().BeLessThan(snapshot.BollingerMiddle!.Value);
    }

    [Fact]
    public async Task CalculateDailyCpr_WithLiveDailyCandles_ProducesAccurateLevels()
    {
        const string key = "NSE_INDEX|Nifty 50";
        var now = DateTime.Now;
        var toDate = now.ToString("yyyy-MM-dd");
        var fromDate = now.AddDays(-7).ToString("yyyy-MM-dd");

        var dailyCandles = await _upstoxClient.GetHistoricalCandlesAsync(key, "day", 1, toDate, fromDate);
        dailyCandles.Should().NotBeEmpty();

        var prevDay = dailyCandles.Count >= 2 ? dailyCandles[^2] : dailyCandles[0];
        var cpr = _indicatorService.CalculateDailyCpr(prevDay.High, prevDay.Low, prevDay.Close);

        cpr.Should().NotBeNull();
        cpr.Pivot.Should().BeGreaterThan(15000m);
        cpr.CprTop.Should().BeGreaterThanOrEqualTo(cpr.CprBottom);
        cpr.R1.Should().BeGreaterThan(cpr.Pivot);
        cpr.S1.Should().BeLessThan(cpr.Pivot);
    }
}
