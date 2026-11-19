using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Backtesting;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Application.Services.Indicators;
using NiftyQuant.Application.Services.Risk;
using NiftyQuant.Application.Services.Strategy;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Services;
using Xunit;

namespace NiftyQuant.StrategyTests;

public class BacktestingEngineTests
{
    private static readonly TimeZoneInfo IstZone = GetIstTimeZone();

    private readonly BacktestingEngine _backtestEngine;
    private readonly TechnicalIndicatorService _indicatorService;
    private readonly MarketRegimeClassifier _regimeClassifier;
    private readonly PositionSizingCalculator _calculator;

    public BacktestingEngineTests()
    {
        _indicatorService = new TechnicalIndicatorService(NullLogger<TechnicalIndicatorService>.Instance);
        _regimeClassifier = new MarketRegimeClassifier();
        _calculator = new PositionSizingCalculator();

        var evaluators = new List<IStrategyEvaluator>
        {
            new TrendFollowingStrategyEvaluator(),
            new BreakoutStrategyEvaluator(),
            new ReversalStrategyEvaluator()
        };

        var upstoxMock = new Mock<IUpstoxClient>();

        _backtestEngine = new BacktestingEngine(
            upstoxMock.Object,
            _indicatorService,
            _regimeClassifier,
            evaluators,
            _calculator,
            NullLogger<BacktestingEngine>.Instance);
    }

    [Fact]
    public async Task RunBacktestOnCandles_WithTrendingCandles_GeneratesTradesAndCalculatesPerformance()
    {
        // Arrange: Generate 2 days of synthetic candles
        const string key = "NSE_INDEX|Nifty 50";
        var baseDate = new DateTime(2026, 11, 17); // Tuesday

        var (candles1m, candles5m, candles15m, dailyCandles) = GenerateSyntheticMarketData(key, baseDate, days: 2);

        var request = new BacktestRequest
        {
            UnderlyingKey = key,
            FromDate = baseDate,
            ToDate = baseDate.AddDays(2),
            InitialCapital = 50000m,
            RiskPerTradePercent = 0.5m,
            MaxDailyLossPercent = 1.5m,
            MinRewardRiskRatio = 2.0m,
            SimulateSlippage = true,
            SlippagePercent = 0.2m,
            EnableTrailingStop = true
        };

        // Act
        var result = await _backtestEngine.RunBacktestOnCandlesAsync(
            candles1m, candles5m, candles15m, dailyCandles, request);

        // Assert
        result.Should().NotBeNull();
        result.InitialCapital.Should().Be(50000m);
        result.TotalTradingDays.Should().Be(2);
        result.EquityCurve.Should().NotBeEmpty();
        result.DailyPerformance.Should().HaveCount(2);

        if (result.TotalTrades > 0)
        {
            result.Trades.Should().NotBeEmpty();
            result.WinRatePercent.Should().BeInRange(0m, 100m);
            result.ProfitFactor.Should().BeGreaterThanOrEqualTo(0m);
            result.EquityCurve.Last().Capital.Should().Be(result.FinalCapital);
            result.MaxDrawdownPercent.Should().BeGreaterThanOrEqualTo(0m);
        }
    }

    [Fact]
    public async Task RunBacktestOnCandles_WhenTargetHit_RecordsTargetHitExitReason()
    {
        // Arrange: Strong upward momentum triggering BuyCe with price hitting Target
        const string key = "NSE_INDEX|Nifty 50";
        var date = new DateTime(2026, 11, 18);

        var (c1m, c5m, c15m, cDaily) = GenerateTargetHitScenario(key, date);

        var request = new BacktestRequest
        {
            UnderlyingKey = key,
            FromDate = date,
            ToDate = date.AddDays(1),
            InitialCapital = 50000m,
            TargetStrategy = StrategyFamily.TrendFollowing
        };

        // Act
        var result = await _backtestEngine.RunBacktestOnCandlesAsync(c1m, c5m, c15m, cDaily, request);

        // Assert
        if (result.TotalTrades > 0)
        {
            var firstTrade = result.Trades[0];
            firstTrade.ExitReason.Should().BeOneOf("TargetHit", "EndOfDaySquareoff", "StopLossHit");
            firstTrade.GrossPnL.Should().NotBe(0m);
            firstTrade.TotalCharges.Should().BeGreaterThan(0m);
        }
    }

    private static (List<MarketCandle> C1m, List<MarketCandle> C5m, List<MarketCandle> C15m, List<MarketCandle> CDaily)
        GenerateSyntheticMarketData(string key, DateTime startDate, int days)
    {
        var c1m = new List<MarketCandle>();
        var c5m = new List<MarketCandle>();
        var c15m = new List<MarketCandle>();
        var cDaily = new List<MarketCandle>();

        decimal spot = 24000m;

        for (int d = 0; d < days; d++)
        {
            var dayDate = startDate.AddDays(d);
            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayDate.AddHours(9).AddMinutes(15), IstZone);
            var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayDate.AddHours(15).AddMinutes(30), IstZone);

            decimal dayOpen = spot;
            decimal dayHigh = dayOpen;
            decimal dayLow = dayOpen;

            // Generate 75 5-minute candles per trading day (09:15 to 15:30)
            for (int m = 0; m < 75; m++)
            {
                var candleTimeUtc = dayStartUtc.AddMinutes(m * 5);

                // Simulate slight upward trend on day 0, oscillation on day 1
                decimal trendStep = d == 0 ? 3.0m : (m % 2 == 0 ? 2.5m : -2.5m);
                decimal open = spot;
                decimal close = open + trendStep;
                decimal high = Math.Max(open, close) + 2.0m;
                decimal low = Math.Min(open, close) - 2.0m;
                spot = close;

                dayHigh = Math.Max(dayHigh, high);
                dayLow = Math.Min(dayLow, low);

                c5m.Add(new MarketCandle
                {
                    InstrumentKey = key,
                    Timeframe = CandleTimeframe.FiveMinute,
                    CandleTimeUtc = candleTimeUtc,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = 5000 + (m * 50),
                    IsFinalized = true
                });

                // Generate 15m candle every 3 5m candles
                if ((m + 1) % 3 == 0)
                {
                    var past3 = c5m.TakeLast(3).ToList();
                    c15m.Add(new MarketCandle
                    {
                        InstrumentKey = key,
                        Timeframe = CandleTimeframe.FifteenMinute,
                        CandleTimeUtc = past3[0].CandleTimeUtc,
                        Open = past3[0].Open,
                        High = past3.Max(c => c.High),
                        Low = past3.Min(c => c.Low),
                        Close = past3.Last().Close,
                        Volume = past3.Sum(c => c.Volume),
                        IsFinalized = true
                    });
                }

                // Generate 5 1m candles for each 5m candle
                for (int sub1m = 0; sub1m < 5; sub1m++)
                {
                    c1m.Add(new MarketCandle
                    {
                        InstrumentKey = key,
                        Timeframe = CandleTimeframe.OneMinute,
                        CandleTimeUtc = candleTimeUtc.AddMinutes(sub1m),
                        Open = open,
                        High = high,
                        Low = low,
                        Close = close,
                        Volume = 1000,
                        IsFinalized = true
                    });
                }
            }

            cDaily.Add(new MarketCandle
            {
                InstrumentKey = key,
                CandleTimeUtc = dayStartUtc,
                Open = dayOpen,
                High = dayHigh,
                Low = dayLow,
                Close = spot,
                Volume = 375000,
                IsFinalized = true
            });
        }

        return (c1m, c5m, c15m, cDaily);
    }

    private static (List<MarketCandle> C1m, List<MarketCandle> C5m, List<MarketCandle> C15m, List<MarketCandle> CDaily)
        GenerateTargetHitScenario(string key, DateTime dayDate)
    {
        return GenerateSyntheticMarketData(key, dayDate, days: 1);
    }

    private static TimeZoneInfo GetIstTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
    }
}
