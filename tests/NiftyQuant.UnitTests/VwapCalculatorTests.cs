using FluentAssertions;
using NiftyQuant.Application.Services.Indicators;
using NiftyQuant.Domain.Entities;
using Xunit;

namespace NiftyQuant.UnitTests;

public class VwapCalculatorTests
{
    [Fact]
    public void Calculate_KnownVolumeAndPrices_ReturnsExactVwap()
    {
        // Arrange
        // Today's session in IST: 09:15, 09:16, 09:17
        var today = DateTime.UtcNow.Date.AddHours(3).AddMinutes(45); // 09:15 IST
        var candles = new List<MarketCandle>
        {
            new()
            {
                InstrumentKey = "NSE_INDEX|Nifty 50",
                CandleTimeUtc = today,
                High = 102m,
                Low = 98m,
                Close = 100m, // TP = 100
                Volume = 1000 // TPV = 100,000
            },
            new()
            {
                InstrumentKey = "NSE_INDEX|Nifty 50",
                CandleTimeUtc = today.AddMinutes(1),
                High = 107m,
                Low = 103m,
                Close = 105m, // TP = 105
                Volume = 2000 // TPV = 210,000
            },
            new()
            {
                InstrumentKey = "NSE_INDEX|Nifty 50",
                CandleTimeUtc = today.AddMinutes(2),
                High = 112m,
                Low = 108m,
                Close = 110m, // TP = 110
                Volume = 1000 // TPV = 110,000
            }
        };

        // Total TPV = 420,000, Total Volume = 4000
        // Expected VWAP = 420,000 / 4,000 = 105.00

        // Act
        var result = VwapCalculator.Calculate(candles);

        // Assert
        result.Vwap.Should().Be(105.00m);
        result.UpperBand1Dev.Should().BeGreaterThan(result.Vwap!.Value);
        result.LowerBand1Dev.Should().BeLessThan(result.Vwap!.Value);
    }
}
