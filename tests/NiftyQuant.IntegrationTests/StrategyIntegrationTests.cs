using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure;
using Xunit;

namespace NiftyQuant.IntegrationTests;

public class StrategyIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IMarketRegimeClassifier _regimeClassifier;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly IOptionsIntelligenceService _optionsIntelligence;
    private readonly IUpstoxClient _upstoxClient;

    public StrategyIntegrationTests()
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
        _strategyEngine = _serviceProvider.GetRequiredService<IStrategyEngine>();
        _regimeClassifier = _serviceProvider.GetRequiredService<IMarketRegimeClassifier>();
        _indicatorService = _serviceProvider.GetRequiredService<ITechnicalIndicatorService>();
        _snapshotEngine = _serviceProvider.GetRequiredService<IOptionChainSnapshotEngine>();
        _optionsIntelligence = _serviceProvider.GetRequiredService<IOptionsIntelligenceService>();
        _upstoxClient = _serviceProvider.GetRequiredService<IUpstoxClient>();
    }

    [Fact]
    public async Task EvaluateStrategies_WithLiveMarketContext_GeneratesAndPersistsSignal()
    {
        // Arrange
        const string key = "NSE_INDEX|Nifty 50";
        var now = DateTime.Now;
        var toDate = now.ToString("yyyy-MM-dd");
        var fromDate = now.AddDays(-5).ToString("yyyy-MM-dd");

        // Fetch real market candles
        var raw15m = await _upstoxClient.GetHistoricalCandlesAsync(key, "minutes", 15, toDate, fromDate);
        var raw5m = await _upstoxClient.GetHistoricalCandlesAsync(key, "minutes", 5, toDate, fromDate);
        var raw1m = await _upstoxClient.GetHistoricalCandlesAsync(key, "minutes", 1, toDate, fromDate);
        var rawDaily = await _upstoxClient.GetHistoricalCandlesAsync(key, "days", 1, toDate, fromDate);

        var candles15m = raw15m.Select(c => new MarketCandle { InstrumentKey = key, Timeframe = CandleTimeframe.FifteenMinute, CandleTimeUtc = c.Timestamp, Open = c.Open, High = c.High, Low = c.Low, Close = c.Close, Volume = c.Volume }).ToList();
        var candles5m = raw5m.Select(c => new MarketCandle { InstrumentKey = key, Timeframe = CandleTimeframe.FiveMinute, CandleTimeUtc = c.Timestamp, Open = c.Open, High = c.High, Low = c.Low, Close = c.Close, Volume = c.Volume }).ToList();
        var candles1m = raw1m.Select(c => new MarketCandle { InstrumentKey = key, Timeframe = CandleTimeframe.OneMinute, CandleTimeUtc = c.Timestamp, Open = c.Open, High = c.High, Low = c.Low, Close = c.Close, Volume = c.Volume }).ToList();

        CprResult? dailyCpr = null;
        decimal prevHigh = 0m, prevLow = 0m, prevClose = 0m;
        if (rawDaily.Count >= 2)
        {
            var prevDay = rawDaily[^2];
            dailyCpr = _indicatorService.CalculateDailyCpr(prevDay.High, prevDay.Low, prevDay.Close);
            prevHigh = prevDay.High;
            prevLow = prevDay.Low;
            prevClose = prevDay.Close;
        }

        var mtf = _indicatorService.CalculateMultiTimeframeState(key, candles1m, candles5m, candles15m, dailyCpr);

        // Capture live option chain snapshot
        var snapshot = await _snapshotEngine.CaptureAndPersistSnapshotAsync(key);
        snapshot.Should().NotBeNull();
        var optionsReport = _optionsIntelligence.AnalyzeSnapshot(snapshot!);

        var currentSpot = snapshot!.UnderlyingPrice > 0m ? snapshot.UnderlyingPrice : (candles1m.Count > 0 ? candles1m[^1].Close : 24000m);

        var context = new StrategyAnalysisContext
        {
            UnderlyingKey = key,
            CurrentPrice = currentSpot,
            MultiTimeframeState = mtf,
            OptionsReport = optionsReport,
            PreviousDayHigh = prevHigh,
            PreviousDayLow = prevLow,
            PreviousDayClose = prevClose,
            // Simulate evaluation during peak market hours (11:00 AM IST = 05:30 UTC) to test full signal processing
            EvaluationTimeUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 5, 30, 0, DateTimeKind.Utc)
        };

        // Act 1: Classify Regime
        var regime = _regimeClassifier.ClassifyRegime(context);
        regime.Should().NotBeNull();
        regime.Regime.Should().BeDefined();
        regime.Confidence.Should().BeGreaterThan(0m);

        // Act 2: Evaluate Strategy Engine
        var signal = await _strategyEngine.EvaluateStrategiesAsync(context);

        // Assert Signal
        signal.Should().NotBeNull();
        signal.UnderlyingKey.Should().Be(key);
        signal.UnderlyingPrice.Should().Be(currentSpot);
        signal.Direction.Should().BeDefined();
        signal.Reasons.Should().NotBeNull();

        // Act 3: Verify Persistence in SQL Server
        var latestSignal = await _strategyEngine.GetLatestSignalAsync(key);
        latestSignal.Should().NotBeNull();
        latestSignal!.UnderlyingKey.Should().Be(key);

        var history = await _strategyEngine.GetSignalHistoryAsync(key, 10);
        history.Should().NotBeEmpty();
    }
}
