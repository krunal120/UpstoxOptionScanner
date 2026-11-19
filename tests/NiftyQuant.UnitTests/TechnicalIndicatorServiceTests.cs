using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Services.Indicators;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using Xunit;

namespace NiftyQuant.UnitTests;

public class TechnicalIndicatorServiceTests
{
    private readonly TechnicalIndicatorService _service = new(NullLogger<TechnicalIndicatorService>.Instance);

    [Fact]
    public void CalculateIndicators_PopulatesAllKeyIndicators()
    {
        // 50 candles in steady uptrend
        var prices = Enumerable.Range(1, 50).Select(i => 24000m + (i * 10m)).ToList();
        var candles = CreateCandles(prices);
        var cpr = CprCalculator.Calculate(24100m, 23900m, 24050m);

        var snapshot = _service.CalculateIndicators(candles, CandleTimeframe.FiveMinute, cpr);

        snapshot.Should().NotBeNull();
        snapshot.Timeframe.Should().Be(CandleTimeframe.FiveMinute);
        snapshot.ClosePrice.Should().Be(24500m);
        snapshot.Ema9.Should().NotBeNull();
        snapshot.Ema21.Should().NotBeNull();
        snapshot.Vwap.Should().NotBeNull();
        snapshot.Supertrend.Should().NotBeNull();
        snapshot.Rsi14.Should().NotBeNull();
        snapshot.Macd.Should().NotBeNull();
        snapshot.Adx14.Should().NotBeNull();
        snapshot.Atr14.Should().NotBeNull();
        snapshot.BollingerUpper.Should().NotBeNull();
        snapshot.DailyCpr.Should().NotBeNull();
    }

    [Fact]
    public void CalculateMultiTimeframeState_StrongBullishSeries_FlagsStrongBullishAlignment()
    {
        // 1m, 5m, 15m all upward trending
        var candles1m = CreateCandles(Enumerable.Range(1, 40).Select(i => 24000m + (i * 5m)));
        var candles5m = CreateCandles(Enumerable.Range(1, 40).Select(i => 24000m + (i * 15m)));
        var candles15m = CreateCandles(Enumerable.Range(1, 40).Select(i => 24000m + (i * 25m)));

        var state = _service.CalculateMultiTimeframeState(
            "NSE_INDEX|Nifty 50", candles1m, candles5m, candles15m);

        state.Should().NotBeNull();
        state.TrendAlignedBullish.Should().BeTrue();
        state.SupertrendAlignedBullish.Should().BeTrue();
        state.AlignmentSummary.Should().Contain("BULLISH");
    }

    [Fact]
    public void CalculateMultiTimeframeState_StrongBearishSeries_FlagsStrongBearishAlignment()
    {
        // 1m, 5m, 15m all downward trending
        var candles1m = CreateCandles(Enumerable.Range(1, 40).Select(i => 25000m - (i * 5m)));
        var candles5m = CreateCandles(Enumerable.Range(1, 40).Select(i => 25000m - (i * 15m)));
        var candles15m = CreateCandles(Enumerable.Range(1, 40).Select(i => 25000m - (i * 25m)));

        var state = _service.CalculateMultiTimeframeState(
            "NSE_INDEX|Nifty 50", candles1m, candles5m, candles15m);

        state.Should().NotBeNull();
        state.TrendAlignedBearish.Should().BeTrue();
        state.SupertrendAlignedBearish.Should().BeTrue();
        state.AlignmentSummary.Should().Contain("BEARISH");
    }

    private static List<MarketCandle> CreateCandles(IEnumerable<decimal> closes)
    {
        var list = new List<MarketCandle>();
        var dt = DateTime.UtcNow.Date.AddHours(3).AddMinutes(45); // 09:15 IST
        int minute = 0;
        foreach (var c in closes)
        {
            list.Add(new MarketCandle
            {
                InstrumentKey = "NSE_INDEX|Nifty 50",
                CandleTimeUtc = dt.AddMinutes(minute++),
                Open = c,
                High = c + 5m,
                Low = c - 5m,
                Close = c,
                Volume = 5000
            });
        }
        return list;
    }
}
