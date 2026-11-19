using FluentAssertions;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Application.Services.Strategy;
using NiftyQuant.Domain.Enums;
using Xunit;

namespace NiftyQuant.UnitTests;

public class TrendFollowingStrategyEvaluatorTests
{
    private readonly TrendFollowingStrategyEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_WhenBullishConditionsAlign_GeneratesBuyCeSignalWithStopsAndTargets()
    {
        // Arrange
        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.StrongBullish,
            TrendAllowedBullish = true,
            TrendAllowedBearish = false
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24100m,
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe15m = new IndicatorSnapshotDto
                {
                    Ema9 = 24080m,
                    Ema21 = 24020m,
                    SupertrendIsBullish = true,
                    Adx14 = 28m
                },
                Timeframe5m = new IndicatorSnapshotDto
                {
                    Ema9 = 24090m,
                    Ema21 = 24050m,
                    Vwap = 24060m,
                    SupertrendIsBullish = true,
                    Rsi14 = 60m // Bullish momentum zone
                }
            },
            OptionsReport = new OptionsIntelligenceReportDto
            {
                OptionsSentimentScore = 40, // Strong bullish sentiment
                PcrAnalysis = new PcrAnalysisDto { PcrOi = 1.25m }
            }
        };

        // Act
        var result = _evaluator.Evaluate(context, regime);

        // Assert
        result.HasSignal.Should().BeTrue();
        result.Direction.Should().BeOneOf(SignalDirection.BuyCe, SignalDirection.StrongBuyCe);
        result.BullishScore.Should().BeGreaterThanOrEqualTo(65m);
        result.StopLossUnderlyingDistance.Should().BeGreaterThanOrEqualTo(20m);
        result.Target1UnderlyingDistance.Should().Be(result.StopLossUnderlyingDistance * 2m);
        result.Target2UnderlyingDistance.Should().Be(result.StopLossUnderlyingDistance * 3m);
        result.Reasons.Should().NotBeEmpty();
    }

    [Fact]
    public void Evaluate_WhenBearishConditionsAlign_GeneratesBuyPeSignal()
    {
        // Arrange
        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.StrongBearish,
            TrendAllowedBullish = false,
            TrendAllowedBearish = true
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 23900m,
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe15m = new IndicatorSnapshotDto
                {
                    Ema9 = 23920m,
                    Ema21 = 23980m,
                    SupertrendIsBullish = false,
                    Adx14 = 30m
                },
                Timeframe5m = new IndicatorSnapshotDto
                {
                    Ema9 = 23890m,
                    Ema21 = 23940m,
                    Vwap = 23950m,
                    SupertrendIsBullish = false,
                    Rsi14 = 40m // Bearish momentum zone
                }
            },
            OptionsReport = new OptionsIntelligenceReportDto
            {
                OptionsSentimentScore = -40, // Bearish sentiment
                PcrAnalysis = new PcrAnalysisDto { PcrOi = 0.65m }
            }
        };

        // Act
        var result = _evaluator.Evaluate(context, regime);

        // Assert
        result.HasSignal.Should().BeTrue();
        result.Direction.Should().BeOneOf(SignalDirection.BuyPe, SignalDirection.StrongBuyPe);
        result.BearishScore.Should().BeGreaterThanOrEqualTo(65m);
        result.StopLossUnderlyingDistance.Should().BeGreaterThanOrEqualTo(20m);
        result.Target1UnderlyingDistance.Should().Be(result.StopLossUnderlyingDistance * 2m);
    }

    [Fact]
    public void Evaluate_WhenRegimeDisallowsBullishAndBearish_ReturnsWait()
    {
        // Arrange
        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.Sideways,
            TrendAllowedBullish = false,
            TrendAllowedBearish = false
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24000m,
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe15m = new IndicatorSnapshotDto { Ema9 = 24005m, Ema21 = 24000m },
                Timeframe5m = new IndicatorSnapshotDto { Ema9 = 24002m, Ema21 = 24000m }
            }
        };

        // Act
        var result = _evaluator.Evaluate(context, regime);

        // Assert
        result.HasSignal.Should().BeFalse();
        result.Direction.Should().Be(SignalDirection.Wait);
    }
}
