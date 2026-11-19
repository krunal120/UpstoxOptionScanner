using FluentAssertions;
using NiftyQuant.Application.Services.Indicators;
using NiftyQuant.Domain.Entities;
using Xunit;

namespace NiftyQuant.UnitTests;

public class BollingerBandsCalculatorTests
{
    [Fact]
    public void Calculate_InsufficientCandles_ReturnsNull()
    {
        var candles = CreateCandles(Enumerable.Range(1, 15).Select(i => 100m + i));
        var result = BollingerBandsCalculator.Calculate(candles, period: 20);

        result.MiddleBand.Should().BeNull();
        result.UpperBand.Should().BeNull();
        result.LowerBand.Should().BeNull();
    }

    [Fact]
    public void Calculate_KnownDistribution_CalculatesSymmetricalBands()
    {
        // 20 candles with identical close prices = 100
        var prices = Enumerable.Repeat(100m, 20).ToList();
        var candles = CreateCandles(prices);

        var result = BollingerBandsCalculator.Calculate(candles, period: 20, stdDevMultiplier: 2.0m);

        result.MiddleBand.Should().Be(100.0m);
        result.UpperBand.Should().Be(100.0m);
        result.LowerBand.Should().Be(100.0m);
        result.Bandwidth.Should().Be(0m);
    }

    [Fact]
    public void Calculate_VolatileSeries_ProducesExpectedBandwidths()
    {
        var prices = Enumerable.Range(1, 20).Select(i => 24000m + (i % 2 == 0 ? 50m : -50m)).ToList();
        var candles = CreateCandles(prices);

        var result = BollingerBandsCalculator.Calculate(candles, period: 20, stdDevMultiplier: 2.0m);

        result.MiddleBand.Should().Be(24000.0m);
        result.UpperBand.Should().BeGreaterThan(result.MiddleBand!.Value);
        result.LowerBand.Should().BeLessThan(result.MiddleBand!.Value);
        result.Bandwidth.Should().BeGreaterThan(0m);
        result.PercentB.Should().BeInRange(0m, 1m);
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
                Volume = 1000
            });
        }
        return list;
    }
}
