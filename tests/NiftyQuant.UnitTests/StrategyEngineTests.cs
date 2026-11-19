using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Services;
using Xunit;

namespace NiftyQuant.UnitTests;

public class StrategyEngineTests
{
    private readonly Mock<IMarketRegimeClassifier> _regimeClassifierMock;
    private readonly Mock<IOptionsIntelligenceService> _optionsIntelligenceMock;
    private readonly IOptions<RiskOptions> _riskOptions;

    public StrategyEngineTests()
    {
        _regimeClassifierMock = new Mock<IMarketRegimeClassifier>();
        _optionsIntelligenceMock = new Mock<IOptionsIntelligenceService>();
        _riskOptions = Options.Create(new RiskOptions
        {
            InitialCapital = 50000m,
            RiskPerTradePercent = 0.5m,
            MaxDailyLossPercent = 1.5m
        });
    }

    [Fact]
    public async Task EvaluateStrategiesAsync_DuringActiveMarketHours_CalculatesContractGreeksAndPremiums()
    {
        // Arrange: 10:30 AM IST = 05:00 UTC
        var evalTimeUtc = new DateTime(2026, 11, 19, 5, 0, 0, DateTimeKind.Utc);

        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.StrongBullish,
            TrendAllowedBullish = true
        };
        _regimeClassifierMock.Setup(r => r.ClassifyRegime(It.IsAny<StrategyAnalysisContext>()))
            .Returns(regime);

        var trendEvaluatorMock = new Mock<IStrategyEvaluator>();
        trendEvaluatorMock.Setup(e => e.Family).Returns(StrategyFamily.TrendFollowing);
        trendEvaluatorMock.Setup(e => e.Evaluate(It.IsAny<StrategyAnalysisContext>(), regime))
            .Returns(new StrategyEvaluationResult
            {
                StrategyFamily = StrategyFamily.TrendFollowing,
                HasSignal = true,
                Direction = SignalDirection.StrongBuyCe,
                BullishScore = 85m,
                Confidence = 85m,
                StopLossUnderlyingDistance = 25m,
                Target1UnderlyingDistance = 50m,
                Target2UnderlyingDistance = 75m,
                Reasons = new List<SignalReasonDto>
                {
                    new() { Category = "Trend", ScoreContribution = 45m, Description = "Bullish trend test" }
                }
            });

        var evaluators = new List<IStrategyEvaluator> { trendEvaluatorMock.Object };

        var engine = new StrategyEngine(
            _regimeClassifierMock.Object,
            evaluators,
            _optionsIntelligenceMock.Object,
            null!, // No scope factory in unit test
            _riskOptions,
            NullLogger<StrategyEngine>.Instance);

        var context = new StrategyAnalysisContext
        {
            UnderlyingKey = "NSE_INDEX|Nifty 50",
            CurrentPrice = 24100m,
            EvaluationTimeUtc = evalTimeUtc,
            OptionsReport = new OptionsIntelligenceReportDto
            {
                RecommendedCe = new SelectedOptionContractDto
                {
                    InstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
                    StrikePrice = 24100m,
                    OptionType = OptionType.CE,
                    ExpiryDate = new DateTime(2026, 11, 26),
                    Ltp = 120.0m,
                    Delta = 0.52m,
                    Theta = -12.5m
                }
            }
        };

        // Act
        var signal = await engine.EvaluateStrategiesAsync(context);

        // Assert
        signal.Should().NotBeNull();
        signal.Direction.Should().Be(SignalDirection.StrongBuyCe);
        signal.SelectedInstrumentKey.Should().Be("NSE_FO|NIFTY26NOV24100CE");
        signal.EntryPremium.Should().Be(120.0m);

        // Expected Stop loss = 120 - (25 * 0.52) = 120 - 13.0 = 107.0
        signal.StopLossPremium.Should().Be(107.0m);
        // Expected Target 1 = 120 + (50 * 0.52) = 120 + 26.0 = 146.0
        signal.Target1Premium.Should().Be(146.0m);
        // Expected Target 2 = 120 + (75 * 0.52) = 120 + 39.0 = 159.0
        signal.Target2Premium.Should().Be(159.0m);

        // Risk Amount = (120 - 107) * 65 = 13 * 65 = 845
        signal.RiskAmount.Should().Be(845.0m);
        signal.RewardToRiskRatio.Should().Be(2.0m);
    }

    [Fact]
    public async Task EvaluateStrategiesAsync_OutsideTradingHours_AppliesTimeOfDayVeto()
    {
        // Arrange: 08:30 AM IST = 03:00 UTC (Pre-market, outside 09:20 - 15:10 IST)
        var preMarketUtc = new DateTime(2026, 11, 19, 3, 0, 0, DateTimeKind.Utc);

        var regime = new MarketRegimeResult
        {
            Regime = MarketRegimeType.StrongBullish,
            TrendAllowedBullish = true
        };
        _regimeClassifierMock.Setup(r => r.ClassifyRegime(It.IsAny<StrategyAnalysisContext>()))
            .Returns(regime);

        var trendEvaluatorMock = new Mock<IStrategyEvaluator>();
        trendEvaluatorMock.Setup(e => e.Evaluate(It.IsAny<StrategyAnalysisContext>(), regime))
            .Returns(new StrategyEvaluationResult
            {
                StrategyFamily = StrategyFamily.TrendFollowing,
                HasSignal = true,
                Direction = SignalDirection.BuyCe,
                BullishScore = 75m,
                StopLossUnderlyingDistance = 25m
            });

        var engine = new StrategyEngine(
            _regimeClassifierMock.Object,
            new[] { trendEvaluatorMock.Object },
            _optionsIntelligenceMock.Object,
            null!,
            _riskOptions,
            NullLogger<StrategyEngine>.Instance);

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24100m,
            EvaluationTimeUtc = preMarketUtc
        };

        // Act
        var signal = await engine.EvaluateStrategiesAsync(context);

        // Assert
        signal.Direction.Should().Be(SignalDirection.NoTrade, "signals outside trading window must be vetoed");
        signal.Reasons.Should().Contain(r => r.Category == "RiskVeto" && r.Description.Contains("Time-of-day veto"));
    }

    [Fact]
    public async Task EvaluateStrategiesAsync_FiresOnSignalGeneratedEvent()
    {
        // Arrange
        var evalTimeUtc = new DateTime(2026, 11, 19, 5, 0, 0, DateTimeKind.Utc); // 10:30 AM IST
        var regime = new MarketRegimeResult { Regime = MarketRegimeType.Sideways };
        _regimeClassifierMock.Setup(r => r.ClassifyRegime(It.IsAny<StrategyAnalysisContext>()))
            .Returns(regime);

        var evaluatorMock = new Mock<IStrategyEvaluator>();
        evaluatorMock.Setup(e => e.Evaluate(It.IsAny<StrategyAnalysisContext>(), regime))
            .Returns(new StrategyEvaluationResult
            {
                StrategyFamily = StrategyFamily.Reversal,
                HasSignal = false,
                Direction = SignalDirection.Wait
            });

        var engine = new StrategyEngine(
            _regimeClassifierMock.Object,
            new[] { evaluatorMock.Object },
            _optionsIntelligenceMock.Object,
            null!,
            _riskOptions,
            NullLogger<StrategyEngine>.Instance);

        StrategySignal? dispatchedSignal = null;
        engine.OnSignalGenerated += s =>
        {
            dispatchedSignal = s;
            return Task.CompletedTask;
        };

        var context = new StrategyAnalysisContext
        {
            CurrentPrice = 24000m,
            EvaluationTimeUtc = evalTimeUtc
        };

        // Act
        var result = await engine.EvaluateStrategiesAsync(context);

        // Assert
        dispatchedSignal.Should().NotBeNull();
        dispatchedSignal!.Id.Should().Be(result.Id);
    }
}
