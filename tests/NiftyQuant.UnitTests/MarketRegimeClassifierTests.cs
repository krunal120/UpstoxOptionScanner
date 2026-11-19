using FluentAssertions;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Application.Services.Strategy;
using NiftyQuant.Domain.Enums;
using Xunit;

namespace NiftyQuant.UnitTests;

public class MarketRegimeClassifierTests
{
    private readonly MarketRegimeClassifier _classifier = new();

    [Fact]
    public void ClassifyRegime_WhenIndiaVixIsHigh_ReturnsHighVolatilityRegime()
    {
        // Arrange
        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24000m,
            IndiaVix = 23.5m,
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe15m = new IndicatorSnapshotDto { Adx14 = 25m, Ema9 = 24100m, Ema21 = 24000m, SupertrendIsBullish = true }
            }
        };

        // Act
        var result = _classifier.ClassifyRegime(context);

        // Assert
        result.Regime.Should().Be(MarketRegimeType.HighVolatility);
        result.TrendAllowedBullish.Should().BeFalse();
        result.TrendAllowedBearish.Should().BeFalse();
        result.ReversalAllowed.Should().BeFalse();
        result.Confidence.Should().Be(85m);
    }

    [Fact]
    public void ClassifyRegime_WhenStrongBullishIndicatorsAlign_ReturnsStrongBullishRegime()
    {
        // Arrange: 15m ADX >= 22, 15m EMA 9 > 21, 15m Supertrend Bullish, Spot >= 5m VWAP
        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24100m,
            IndiaVix = 14.0m,
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe15m = new IndicatorSnapshotDto
                {
                    Adx14 = 28m,
                    Ema9 = 24050m,
                    Ema21 = 23980m,
                    SupertrendIsBullish = true
                },
                Timeframe5m = new IndicatorSnapshotDto
                {
                    Ema9 = 24080m,
                    Ema21 = 24020m,
                    Vwap = 24040m
                }
            }
        };

        // Act
        var result = _classifier.ClassifyRegime(context);

        // Assert
        result.Regime.Should().Be(MarketRegimeType.StrongBullish);
        result.TrendAllowedBullish.Should().BeTrue();
        result.TrendAllowedBearish.Should().BeFalse();
        result.ReversalAllowed.Should().BeFalse("should never fade a strong trend");
        result.Confidence.Should().BeGreaterThan(80m);
    }

    [Fact]
    public void ClassifyRegime_WhenStrongBearishIndicatorsAlign_ReturnsStrongBearishRegime()
    {
        // Arrange: 15m ADX >= 22, 15m EMA 9 < 21, 15m Supertrend Bearish, Spot <= 5m VWAP
        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 23800m,
            IndiaVix = 16.5m,
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe15m = new IndicatorSnapshotDto
                {
                    Adx14 = 30m,
                    Ema9 = 23850m,
                    Ema21 = 23950m,
                    SupertrendIsBullish = false
                },
                Timeframe5m = new IndicatorSnapshotDto
                {
                    Ema9 = 23820m,
                    Ema21 = 23890m,
                    Vwap = 23860m
                }
            }
        };

        // Act
        var result = _classifier.ClassifyRegime(context);

        // Assert
        result.Regime.Should().Be(MarketRegimeType.StrongBearish);
        result.TrendAllowedBullish.Should().BeFalse();
        result.TrendAllowedBearish.Should().BeTrue();
        result.ReversalAllowed.Should().BeFalse();
        result.Confidence.Should().BeGreaterThan(80m);
    }

    [Fact]
    public void ClassifyRegime_WhenAdxIsWeak_ReturnsSidewaysRegime()
    {
        // Arrange: 15m ADX < 19
        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24000m,
            IndiaVix = 13.0m,
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe15m = new IndicatorSnapshotDto
                {
                    Adx14 = 15m,
                    Ema9 = 24010m,
                    Ema21 = 23990m,
                    SupertrendIsBullish = true
                },
                Timeframe5m = new IndicatorSnapshotDto
                {
                    Ema9 = 24005m,
                    Ema21 = 24000m,
                    Vwap = 24000m
                }
            }
        };

        // Act
        var result = _classifier.ClassifyRegime(context);

        // Assert
        result.Regime.Should().Be(MarketRegimeType.Sideways);
        result.TrendAllowedBullish.Should().BeFalse();
        result.TrendAllowedBearish.Should().BeFalse();
        result.ReversalAllowed.Should().BeTrue("reversals are permitted in sideways market");
    }
}
