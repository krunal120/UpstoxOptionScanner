using FluentAssertions;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Application.Services.Strategy;
using NiftyQuant.Domain.Enums;
using Xunit;

namespace NiftyQuant.UnitTests;

public class BreakoutStrategyEvaluatorTests
{
    private readonly BreakoutStrategyEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_WhenBreaksCallResistanceWithVolumeSurge_GeneratesBuyCe()
    {
        // Arrange
        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.StrongBullish,
            TrendAllowedBullish = true
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24210m,
            PreviousDayHigh = 24180m,
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe5m = new IndicatorSnapshotDto
                {
                    RelativeVolume = 1.6m // Institutional surge
                },
                Timeframe15m = new IndicatorSnapshotDto
                {
                    Adx14 = 26m
                }
            },
            OptionsReport = new OptionsIntelligenceReportDto
            {
                KeyLevels = new SupportResistanceLevelsDto
                {
                    MajorCallResistance = 24200m // Breached!
                },
                PcrAnalysis = new PcrAnalysisDto { PcrOi = 1.15m },
                OptionsSentimentScore = 25
            }
        };

        // Act
        var result = _evaluator.Evaluate(context, regime);

        // Assert
        result.HasSignal.Should().BeTrue();
        result.Direction.Should().BeOneOf(SignalDirection.BuyCe, SignalDirection.StrongBuyCe);
        result.BullishScore.Should().BeGreaterThanOrEqualTo(65m);
        result.Reasons.Should().Contain(r => r.Category == "PriceAction");
        result.Reasons.Should().Contain(r => r.Category == "VolumeBreadth");
    }

    [Fact]
    public void Evaluate_WhenBreaksPutFloorWithVolumeSurge_GeneratesBuyPe()
    {
        // Arrange
        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.StrongBearish,
            TrendAllowedBearish = true
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 23790m,
            PreviousDayLow = 23820m,
            MultiTimeframeState = new MultiTimeframeIndicatorState
            {
                Timeframe5m = new IndicatorSnapshotDto
                {
                    RelativeVolume = 1.5m
                },
                Timeframe15m = new IndicatorSnapshotDto
                {
                    Adx14 = 25m
                }
            },
            OptionsReport = new OptionsIntelligenceReportDto
            {
                KeyLevels = new SupportResistanceLevelsDto
                {
                    MajorPutSupport = 23800m // Breakdown below put floor!
                },
                PcrAnalysis = new PcrAnalysisDto { PcrOi = 0.60m },
                OptionsSentimentScore = -25
            }
        };

        // Act
        var result = _evaluator.Evaluate(context, regime);

        // Assert
        result.HasSignal.Should().BeTrue();
        result.Direction.Should().BeOneOf(SignalDirection.BuyPe, SignalDirection.StrongBuyPe);
        result.BearishScore.Should().BeGreaterThanOrEqualTo(65m);
    }

    [Fact]
    public void Evaluate_WhenHighVolatilityRegime_VetoesBreakouts()
    {
        // Arrange
        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.HighVolatility
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24210m,
            PreviousDayHigh = 24200m
        };

        // Act
        var result = _evaluator.Evaluate(context, regime);

        // Assert
        result.HasSignal.Should().BeFalse();
        result.Direction.Should().Be(SignalDirection.Wait);
    }
}
