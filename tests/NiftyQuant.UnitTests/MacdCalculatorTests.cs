using FluentAssertions;
using NiftyQuant.Application.Services.Indicators;
using NiftyQuant.Domain.Entities;
using Xunit;

namespace NiftyQuant.UnitTests;

public class MacdCalculatorTests
{
    [Fact]
    public void Calculate_InsufficientCandles_ReturnsNull()
    {
        var candles = CreateCandles(Enumerable.Range(1, 20).Select(i => 100m + i));
        var result = MacdCalculator.Calculate(candles);

        result.MacdLine.Should().BeNull();
        result.SignalLine.Should().BeNull();
        result.Histogram.Should().BeNull();
    }

    [Fact]
    public void Calculate_TrendingPrices_ProducesConsistentMacdAndHistogram()
    {
        // 50 prices steadily rising
        var prices = Enumerable.Range(1, 50).Select(i => 100m + (i * 1.5m)).ToList();
        var candles = CreateCandles(prices);

        var result = MacdCalculator.Calculate(candles, fastPeriod: 12, slowPeriod: 26, signalPeriod: 9);

        result.MacdLine.Should().NotBeNull();
        result.SignalLine.Should().NotBeNull();
        result.Histogram.Should().NotBeNull();

        // In a steady uptrend, Fast EMA > Slow EMA, so MACD line > 0
        result.MacdLine!.Value.Should().BeGreaterThan(0m);
        // Histogram must match difference
        result.Histogram!.Value.Should().Be(Math.Round(result.MacdLine.Value - result.SignalLine!.Value, 4));
    }

    private static List<MarketCandle> CreateCandles(IEnumerable<decimal> closes)
    {
        var list = new List<MarketCandle>();
        var dt = new DateTime(2026, 11, 19, 9, 15, 0, DateTimeKind.Utc);
        int minute = 0;
        foreach (var c in closes)
        {
            list.Add(new MarketCandle
            {
                InstrumentKey = "NSE_INDEX|Nifty 50",
                CandleTimeUtc = dt.AddMinutes(minute++),
                Open = c,
                High = c + 1m,
                Low = c - 1m,
                Close = c,
                Volume = 1000
            });
        }
        return list;
    }
}
