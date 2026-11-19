using FluentAssertions;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using Xunit;

namespace NiftyQuant.UnitTests;

public class FoundationTests
{
    [Fact]
    public void PaperAccount_DefaultInitialization_ShouldMatchRiskParameters()
    {
        // Arrange & Act
        var account = new PaperAccount();

        // Assert
        account.InitialCapital.Should().Be(50000.00m);
        account.AvailableBalance.Should().Be(50000.00m);
        account.MaxRiskPerTrade.Should().Be(250.00m); // 0.5% of 50k
        account.DailyLossLimit.Should().Be(750.00m);  // 1.5% of 50k
        account.IsTradingLockedForDay.Should().BeFalse();
    }

    [Fact]
    public void StrategySignal_DefaultValues_ShouldBeConsistent()
    {
        // Arrange & Act
        var signal = new StrategySignal
        {
            UnderlyingKey = "NSE_INDEX|Nifty 50",
            Direction = SignalDirection.BuyCe,
            StrategyFamily = StrategyFamily.TrendFollowing,
            MarketRegime = MarketRegimeType.StrongBullish,
            BullishScore = 85.0m,
            BearishScore = 20.0m,
            Confidence = 0.85m,
            EntryPremium = 120.0m,
            StopLossPremium = 100.0m,
            Target1Premium = 160.0m,
            LotSize = 50,
            RecommendedQuantity = 50
        };

        // Assert
        signal.BullishScore.Should().BeGreaterThan(signal.BearishScore);
        signal.EntryPremium.Should().BeGreaterThan(signal.StopLossPremium);
        signal.Target1Premium.Should().BeGreaterThan(signal.EntryPremium);
        signal.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void RiskOptions_ShouldHaveSensibleDefaults()
    {
        // Arrange & Act
        var options = new RiskOptions();

        // Assert
        options.InitialCapital.Should().Be(50000m);
        options.RiskPerTradePercent.Should().Be(0.5m);
        options.MaxDailyLossPercent.Should().Be(1.5m);
        options.MinRewardRiskRatio.Should().Be(2.0m);
        options.MaxConcurrentPositions.Should().Be(1);
    }
}
