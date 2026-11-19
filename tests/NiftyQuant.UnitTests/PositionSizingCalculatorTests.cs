using FluentAssertions;
using NiftyQuant.Application.Services.Risk;
using Xunit;

namespace NiftyQuant.UnitTests;

public class PositionSizingCalculatorTests
{
    private readonly PositionSizingCalculator _calculator = new();

    [Fact]
    public void CalculatePositionSize_WithSufficientCapital_AllocatesOneLotAndCalculatesRisk()
    {
        // Arrange
        decimal entryPremium = 120.0m;
        decimal stopLossPremium = 100.0m;
        decimal availableBalance = 50000.0m;
        decimal maxRiskAllowed = 250.0m;

        // Act
        var result = _calculator.CalculatePositionSize(
            entryPremium, stopLossPremium, availableBalance, maxRiskAllowed);

        // Assert
        result.IsAffordable.Should().BeTrue();
        result.AllocatedLots.Should().Be(1);
        result.AllocatedQuantity.Should().Be(65); // NIFTY lot size
        result.RiskPerShare.Should().Be(20.0m);
        result.TotalRiskAmount.Should().Be(1300.0m); // 20 * 65
        result.TotalCost.Should().Be(7800.0m); // 120 * 65
    }

    [Fact]
    public void CalculatePositionSize_WhenBalanceIsInsufficient_ReturnsNotAffordable()
    {
        // Arrange
        decimal entryPremium = 150.0m;
        decimal stopLossPremium = 130.0m;
        decimal availableBalance = 5000.0m; // Less than 150 * 65 = 9750
        decimal maxRiskAllowed = 250.0m;

        // Act
        var result = _calculator.CalculatePositionSize(
            entryPremium, stopLossPremium, availableBalance, maxRiskAllowed);

        // Assert
        result.IsAffordable.Should().BeFalse();
        result.AllocatedLots.Should().Be(0);
        result.RejectionReason.Should().Contain("Insufficient balance");
    }

    [Fact]
    public void CalculatePositionSize_WhenStopLossIsAboveOrEqualEntry_ReturnsRejection()
    {
        // Arrange
        decimal entryPremium = 100.0m;
        decimal stopLossPremium = 105.0m;

        // Act
        var result = _calculator.CalculatePositionSize(
            entryPremium, stopLossPremium, 50000m, 250m);

        // Assert
        result.IsAffordable.Should().BeFalse();
        result.RejectionReason.Should().Contain("strictly less than entry premium");
    }

    [Theory]
    [InlineData(100, 80, 140, true, 2.0)]   // Risk=20, Reward=40 -> 1:2.0 (Valid)
    [InlineData(100, 80, 160, true, 3.0)]   // Risk=20, Reward=60 -> 1:3.0 (Valid)
    [InlineData(100, 80, 130, false, 1.5)]  // Risk=20, Reward=30 -> 1:1.5 (Invalid)
    [InlineData(100, 90, 105, false, 0.5)]  // Risk=10, Reward=5  -> 1:0.5 (Invalid)
    public void ValidateRewardToRisk_EvaluatesThresholdCorrectly(
        decimal entry, decimal sl, decimal target, bool expectedValid, decimal expectedRatio)
    {
        // Act
        var (isValid, ratio) = _calculator.ValidateRewardToRisk(entry, sl, target, minRatio: 2.0m);

        // Assert
        isValid.Should().Be(expectedValid);
        ratio.Should().Be(expectedRatio);
    }

    [Fact]
    public void CalculateEstimatedRoundTripCharges_ComputesBrokerageTaxesAndRegulatoryLevies()
    {
        // Arrange: Buy 65 shares @ 100, Sell 65 shares @ 140
        decimal entryPremium = 100.0m;
        decimal exitPremium = 140.0m;
        int quantity = 65;

        // Act
        var charges = _calculator.CalculateEstimatedRoundTripCharges(entryPremium, exitPremium, quantity);

        // Assert
        // Turnover = (100 + 140) * 65 = 15,600
        // Brokerage = 40.0
        // STT (0.1% on sell 9,100) = 9.10
        // Exchange charges (0.05% on 15,600) = 7.80
        // GST (18% on 47.80) = 8.60
        // Stamp duty = 0.20
        // Total should be approximately 65-70 INR
        charges.Should().BeInRange(50.0m, 80.0m);
    }
}
