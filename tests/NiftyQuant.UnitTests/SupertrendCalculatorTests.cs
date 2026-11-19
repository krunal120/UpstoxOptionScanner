using FluentAssertions;
using NiftyQuant.Application.Services.Indicators;
using NiftyQuant.Domain.Entities;
using Xunit;

namespace NiftyQuant.UnitTests;

public class SupertrendCalculatorTests
{
    [Fact]
    public void Calculate_InsufficientCandles_ReturnsNull()
    {
        var candles = CreateCandles(Enumerable.Range(1, 5).Select(i => 100m + i));
        var result = SupertrendCalculator.Calculate(candles, period: 10, multiplier: 3.0m);

        result.Supertrend.Should().BeNull();
    }

    [Fact]
    public void Calculate_SteadyUptrend_SupertrendIsBullishAndBelowPrice()
    {
        // 25 candles in clear uptrend
        var prices = Enumerable.Range(1, 25).Select(i => 24000m + (i * 20m)).ToList();
        var candles = CreateCandles(prices);

        var result = SupertrendCalculator.Calculate(candles, period: 10, multiplier: 3.0m);

        result.Supertrend.Should().NotBeNull();
        result.IsBullish.Should().BeTrue();
        result.Supertrend!.Value.Should().BeLessThan(candles[^1].Close);
    }

    [Fact]
    public void Calculate_SteadyDowntrend_SupertrendIsBearishAndAbovePrice()
    {
        // 25 candles in clear downtrend
        var prices = Enumerable.Range(1, 25).Select(i => 25000m - (i * 20m)).ToList();
        var candles = CreateCandles(prices);

        var result = SupertrendCalculator.Calculate(candles, period: 10, multiplier: 3.0m);

        result.Supertrend.Should().NotBeNull();
        result.IsBullish.Should().BeFalse();
        result.Supertrend!.Value.Should().BeGreaterThan(candles[^1].Close);
    }

    [Fact]
    public void Calculate_ReversalFromBullToBear_FlipsDirection()
    {
        // 20 candles up, followed by a massive drop
        var prices = new List<decimal>();
        for (int i = 1; i <= 20; i++) prices.Add(25000m + (i * 10m)); // 25010 to 25200
        // Sharp drop below ATR bands
        prices.Add(24500m);
        prices.Add(24300m);

        var candles = CreateCandles(prices);
        var result = SupertrendCalculator.Calculate(candles, period: 10, multiplier: 3.0m);

        result.IsBullish.Should().BeFalse();
        result.Supertrend!.Value.Should().BeGreaterThan(candles[^1].Close);
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
                High = c + 5m,
                Low = c - 5m,
                Close = c,
                Volume = 5000
            });
        }
        return list;
    }
}
