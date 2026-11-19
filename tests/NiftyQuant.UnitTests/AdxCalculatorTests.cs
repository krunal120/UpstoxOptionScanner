using FluentAssertions;
using NiftyQuant.Application.Services.Indicators;
using NiftyQuant.Domain.Entities;
using Xunit;

namespace NiftyQuant.UnitTests;

public class AdxCalculatorTests
{
    [Fact]
    public void Calculate_InsufficientCandles_ReturnsNull()
    {
        var candles = CreateCandles(Enumerable.Range(1, 10).Select(i => 100m + i));
        var result = AdxCalculator.Calculate(candles, period: 14);

        result.Adx.Should().BeNull();
        result.PlusDi.Should().BeNull();
        result.MinusDi.Should().BeNull();
    }

    [Fact]
    public void Calculate_StrongUptrend_PlusDiGreaterThanMinusDi()
    {
        // 40 candles in consistent uptrend
        var prices = Enumerable.Range(1, 40).Select(i => 24000m + (i * 25m)).ToList();
        var candles = CreateCandles(prices);

        var result = AdxCalculator.Calculate(candles, period: 14);

        result.Adx.Should().NotBeNull();
        result.PlusDi.Should().NotBeNull();
        result.MinusDi.Should().NotBeNull();

        result.PlusDi!.Value.Should().BeGreaterThan(result.MinusDi!.Value);
        result.Adx!.Value.Should().BeGreaterThan(25m); // Strong trend threshold
    }

    [Fact]
    public void Calculate_StrongDowntrend_MinusDiGreaterThanPlusDi()
    {
        // 40 candles in consistent downtrend
        var prices = Enumerable.Range(1, 40).Select(i => 25000m - (i * 25m)).ToList();
        var candles = CreateCandles(prices);

        var result = AdxCalculator.Calculate(candles, period: 14);

        result.Adx.Should().NotBeNull();
        result.PlusDi.Should().NotBeNull();
        result.MinusDi.Should().NotBeNull();

        result.MinusDi!.Value.Should().BeGreaterThan(result.PlusDi!.Value);
        result.Adx!.Value.Should().BeGreaterThan(25m);
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
                High = c + 10m,
                Low = c - 10m,
                Close = c,
                Volume = 5000
            });
        }
        return list;
    }
}
