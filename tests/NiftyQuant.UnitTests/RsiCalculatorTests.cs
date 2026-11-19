using FluentAssertions;
using NiftyQuant.Application.Services.Indicators;
using NiftyQuant.Domain.Entities;
using Xunit;

namespace NiftyQuant.UnitTests;

public class RsiCalculatorTests
{
    [Fact]
    public void Calculate_InsufficientCandles_ReturnsNull()
    {
        var candles = CreateCandles(new[] { 100m, 102m, 101m });
        var result = RsiCalculator.Calculate(candles, period: 14);
        result.Rsi.Should().BeNull();
    }

    [Fact]
    public void Calculate_StrictlyBullishTrend_RsiIsVeryHigh()
    {
        // 30 consecutive candles where close increases steadily
        var prices = Enumerable.Range(1, 30).Select(i => 100m + (i * 2m)).ToArray();
        var candles = CreateCandles(prices);

        var result = RsiCalculator.Calculate(candles, period: 14);

        result.Rsi.Should().NotBeNull();
        result.Rsi!.Value.Should().Be(100m);
    }

    [Fact]
    public void Calculate_StrictlyBearishTrend_RsiIsZero()
    {
        // 30 consecutive candles where close decreases steadily
        var prices = Enumerable.Range(1, 30).Select(i => 200m - (i * 2m)).ToArray();
        var candles = CreateCandles(prices);

        var result = RsiCalculator.Calculate(candles, period: 14);

        result.Rsi.Should().NotBeNull();
        result.Rsi!.Value.Should().Be(0m);
    }

    [Fact]
    public void Calculate_OscillatingPrices_RsiIsAroundFifty()
    {
        // Alternating +2 and -2 for 30 bars
        var prices = new List<decimal> { 100m };
        for (int i = 1; i < 30; i++)
        {
            prices.Add(i % 2 == 1 ? 102m : 100m);
        }
        var candles = CreateCandles(prices);

        var result = RsiCalculator.Calculate(candles, period: 14);

        result.Rsi.Should().NotBeNull();
        result.Rsi!.Value.Should().BeInRange(45m, 55m);
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
