using FluentAssertions;
using NiftyQuant.Application.Services.Indicators;
using Xunit;

namespace NiftyQuant.UnitTests;

public class MovingAverageCalculatorTests
{
    [Fact]
    public void CalculateSma_WithValidPrices_ReturnsCorrectAverage()
    {
        // Arrange
        var prices = new List<decimal> { 10m, 20m, 30m, 40m, 50m };

        // Act
        var sma3 = MovingAverageCalculator.CalculateSma(prices, 3); // (30 + 40 + 50) / 3 = 40
        var sma5 = MovingAverageCalculator.CalculateSma(prices, 5); // (10 + 20 + 30 + 40 + 50) / 5 = 30

        // Assert
        sma3.Should().Be(40m);
        sma5.Should().Be(30m);
    }

    [Fact]
    public void CalculateSma_InsufficientPrices_ReturnsNull()
    {
        var prices = new List<decimal> { 10m, 20m };
        var sma5 = MovingAverageCalculator.CalculateSma(prices, 5);
        sma5.Should().BeNull();
    }

    [Fact]
    public void CalculateEma_ValidPrices_MatchesStandardFormula()
    {
        // Arrange: 5 prices for period 3
        // Multiplier = 2 / (3 + 1) = 0.5
        // Prices: 10, 11, 12, 14, 16
        // Initial EMA (at index 2) = SMA(10, 11, 12) = 11.0
        // Next EMA (at index 3, price 14) = (14 * 0.5) + (11.0 * 0.5) = 7.0 + 5.5 = 12.5
        // Next EMA (at index 4, price 16) = (16 * 0.5) + (12.5 * 0.5) = 8.0 + 6.25 = 14.25
        var prices = new List<decimal> { 10m, 11m, 12m, 14m, 16m };

        // Act
        var series = MovingAverageCalculator.CalculateEmaSeries(prices, 3);
        var latest = MovingAverageCalculator.CalculateLatestEma(prices, 3);

        // Assert
        series.Should().HaveCount(3);
        series[0].Should().Be(11.0m);
        series[1].Should().Be(12.5m);
        series[2].Should().Be(14.25m);
        latest.Should().Be(14.25m);
    }
}
