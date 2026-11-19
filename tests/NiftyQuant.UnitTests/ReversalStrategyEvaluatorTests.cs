using FluentAssertions;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Application.Services.Strategy;
using NiftyQuant.Domain.Enums;
using Xunit;

namespace NiftyQuant.UnitTests;

public class ReversalStrategyEvaluatorTests
{
    private readonly ReversalStrategyEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_WhenReversalDisallowedByRegime_ReturnsWaitImmediately()
    {
        // Arrange
        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.StrongBullish,
            ReversalAllowed = false // Never fade a strong trend
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24200m
        };

        // Act
        var result = _evaluator.Evaluate(context, regime);

        // Assert
        result.HasSignal.Should().BeFalse();
        result.Direction.Should().Be(SignalDirection.Wait);
    }

    [Fact]
    public void Evaluate_WhenInSidewaysRegimeAndAtExtremePutSupport_GeneratesBuyCe()
    {
        // Arrange
        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.Sideways,
            ReversalAllowed = true
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 23805m, // At Put Support 23800
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe5m = new IndicatorSnapshotDto
                {
                    BollingerLower = 23800m,
                    Rsi14 = 28m // Oversold exhaustion
                }
            },
            OptionsReport = new OptionsIntelligenceReportDto
            {
                KeyLevels = new SupportResistanceLevelsDto
                {
                    MajorPutSupport = 23800m
                },
                PcrAnalysis = new PcrAnalysisDto
                {
                    PcrOi = 0.70m // Oversold PCR
                }
            }
        };

        // Act
        var result = _evaluator.Evaluate(context, regime);

        // Assert
        result.HasSignal.Should().BeTrue();
        result.Direction.Should().Be(SignalDirection.BuyCe);
        result.BullishScore.Should().BeGreaterThanOrEqualTo(65m);
        result.Reasons.Should().Contain(r => r.Category == "PriceAction");
        result.Reasons.Should().Contain(r => r.Category == "Momentum");
    }

    [Fact]
    public void Evaluate_WhenInSidewaysRegimeAndAtExtremeCallResistance_GeneratesBuyPe()
    {
        // Arrange
        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.Sideways,
            ReversalAllowed = true
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24195m, // Near Call Resistance 24200
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe5m = new IndicatorSnapshotDto
                {
                    BollingerUpper = 24200m,
                    Rsi14 = 72m // Overbought exhaustion
                }
            },
            OptionsReport = new OptionsIntelligenceReportDto
            {
                KeyLevels = new SupportResistanceLevelsDto
                {
                    MajorCallResistance = 24200m
                },
                PcrAnalysis = new PcrAnalysisDto
                {
                    PcrOi = 1.40m // Overbought PCR
                }
            }
        };

        // Act
        var result = _evaluator.Evaluate(context, regime);

        // Assert
        result.HasSignal.Should().BeTrue();
        result.Direction.Should().Be(SignalDirection.BuyPe);
        result.BearishScore.Should().BeGreaterThanOrEqualTo(65m);
    }
}
