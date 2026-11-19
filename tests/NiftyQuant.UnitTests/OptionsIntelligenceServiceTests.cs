using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Services.Options;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using Xunit;

namespace NiftyQuant.UnitTests;

public class OptionsIntelligenceServiceTests
{
    private readonly OptionsIntelligenceService _service = new(NullLogger<OptionsIntelligenceService>.Instance);

    [Theory]
    [InlineData(10.0, 50000.0, OiBuildupType.LongBuildup)]
    [InlineData(-10.0, 50000.0, OiBuildupType.ShortBuildup)]
    [InlineData(10.0, -50000.0, OiBuildupType.ShortCovering)]
    [InlineData(-10.0, -50000.0, OiBuildupType.LongUnwinding)]
    [InlineData(0.0, 0.0, OiBuildupType.Neutral)]
    public void ClassifyBuildup_AllScenarios_ReturnsExpectedClassification(
        decimal priceChange, decimal oiChange, OiBuildupType expected)
    {
        var result = _service.ClassifyBuildup(priceChange, oiChange);
        result.Should().Be(expected);
    }

    [Fact]
    public void AnalyzePcr_BullishRising_CalculatesCorrectMetrics()
    {
        // 1,200,000 Put OI vs 1,000,000 Call OI -> PCR = 1.20
        // Previous PCR = 1.10 -> Delta = +0.10 (Rising)
        var pcr = _service.AnalyzePcr(
            totalPutOi: 1200000m,
            totalCallOi: 1000000m,
            totalPutVol: 500000m,
            totalCallVol: 400000m,
            previousPcr: 1.10m);

        pcr.PcrOi.Should().Be(1.2000m);
        pcr.PcrVolume.Should().Be(1.2500m);
        pcr.PcrChange.Should().Be(0.1000m);
        pcr.PcrTrend.Should().Be("Rising");
        pcr.SentimentZone.Should().Be("Strongly Bullish");
    }

    [Fact]
    public void AnalyzePcr_OversoldFalling_CalculatesCorrectMetrics()
    {
        var pcr = _service.AnalyzePcr(
            totalPutOi: 500000m,
            totalCallOi: 1000000m,
            totalPutVol: 300000m,
            totalCallVol: 600000m,
            previousPcr: 0.60m);

        pcr.PcrOi.Should().Be(0.5000m);
        pcr.PcrTrend.Should().Be("Falling");
        pcr.SentimentZone.Should().Contain("Oversold");
    }

    [Fact]
    public void IdentifySupportResistance_IdentifiesMajorAndImmediateWalls()
    {
        const decimal spot = 25025m;
        const decimal maxPain = 25000m;

        var strikes = new List<StrikeAnalysisDto>
        {
            new() { StrikePrice = 24800m, PutOi = 500000m, CallOi = 50000m },
            new() { StrikePrice = 24900m, PutOi = 1200000m, CallOi = 100000m }, // Major Put Support
            new() { StrikePrice = 25000m, PutOi = 800000m, CallOi = 600000m },  // Immediate Put Support below spot
            new() { StrikePrice = 25100m, PutOi = 400000m, CallOi = 900000m },  // Immediate Call Resistance above spot
            new() { StrikePrice = 25200m, PutOi = 100000m, CallOi = 1500000m }, // Major Call Resistance
        };

        var levels = _service.IdentifySupportResistance(spot, maxPain, strikes);

        levels.SpotPrice.Should().Be(spot);
        levels.MaxPainStrike.Should().Be(maxPain);
        levels.MajorCallResistance.Should().Be(25200m);
        levels.MajorPutSupport.Should().Be(24900m);
        levels.ImmediateCallResistance.Should().Be(25100m);
        levels.ImmediatePutSupport.Should().Be(25000m);
    }

    [Fact]
    public void SelectBestContract_CeBuying_SelectsLiquidAtmContractWithOptimalDelta()
    {
        const decimal spot = 25035m; // Nearest ATM is 25050
        var today = DateTime.UtcNow.Date;

        var quotes = new List<OptionQuote>
        {
            // Strike 24800 CE: Deep ITM, high price, low delta distance but wider spread
            new()
            {
                InstrumentKey = "NSE_FO|101",
                OptionType = OptionType.CE,
                StrikePrice = 24800m,
                ExpiryDate = today,
                Ltp = 280m,
                BidPrice = 278m,
                AskPrice = 283m,
                Volume = 5000,
                OpenInterest = 100000m,
                Delta = 0.85m
            },
            // Strike 25050 CE: ATM, optimal delta ~0.52, high volume, tight spread
            new()
            {
                InstrumentKey = "NSE_FO|102",
                OptionType = OptionType.CE,
                StrikePrice = 25050m,
                ExpiryDate = today,
                Ltp = 120m,
                BidPrice = 119.8m,
                AskPrice = 120.2m,
                Volume = 150000,
                OpenInterest = 1500000m,
                Delta = 0.52m,
                ImpliedVolatility = 14.2m
            },
            // Strike 25300 CE: OTM, low delta 0.20
            new()
            {
                InstrumentKey = "NSE_FO|103",
                OptionType = OptionType.CE,
                StrikePrice = 25300m,
                ExpiryDate = today,
                Ltp = 30m,
                BidPrice = 29.5m,
                AskPrice = 30.5m,
                Volume = 20000,
                OpenInterest = 600000m,
                Delta = 0.20m
            }
        };

        var selected = _service.SelectBestContract(quotes, OptionType.CE, spot);

        selected.Should().NotBeNull();
        selected!.InstrumentKey.Should().Be("NSE_FO|102");
        selected.StrikePrice.Should().Be(25050m);
        selected.OptionType.Should().Be(OptionType.CE);
        selected.Delta.Should().Be(0.52m);
        selected.LiquidityScore.Should().BeGreaterThan(70m);
        selected.IsRecommended.Should().BeTrue();
    }

    [Fact]
    public void SelectBestContract_PeBuying_SelectsLiquidAtmContractWithOptimalDelta()
    {
        const decimal spot = 25035m;
        var today = DateTime.UtcNow.Date;

        var quotes = new List<OptionQuote>
        {
            // Strike 25050 PE: ATM, optimal delta -0.48, tight spread, high volume
            new()
            {
                InstrumentKey = "NSE_FO|201",
                OptionType = OptionType.PE,
                StrikePrice = 25050m,
                ExpiryDate = today,
                Ltp = 115m,
                BidPrice = 114.8m,
                AskPrice = 115.2m,
                Volume = 180000,
                OpenInterest = 1800000m,
                Delta = -0.48m,
                ImpliedVolatility = 13.8m
            },
            // Strike 24700 PE: Far OTM
            new()
            {
                InstrumentKey = "NSE_FO|202",
                OptionType = OptionType.PE,
                StrikePrice = 24700m,
                ExpiryDate = today,
                Ltp = 18m,
                BidPrice = 17.5m,
                AskPrice = 18.5m,
                Volume = 10000,
                OpenInterest = 400000m,
                Delta = -0.15m
            }
        };

        var selected = _service.SelectBestContract(quotes, OptionType.PE, spot);

        selected.Should().NotBeNull();
        selected!.InstrumentKey.Should().Be("NSE_FO|201");
        selected.StrikePrice.Should().Be(25050m);
        selected.OptionType.Should().Be(OptionType.PE);
        selected.Delta.Should().Be(-0.48m);
    }

    [Fact]
    public void CalculateSentimentScore_BullishInputs_ProducesPositiveBullishScore()
    {
        var pcr = new PcrAnalysisDto { PcrOi = 1.35m };
        var levels = new SupportResistanceLevelsDto { MaxPainStrike = 24900m };
        const decimal spot = 25050m; // Spot 150 pts above Max Pain

        var strikes = new List<StrikeAnalysisDto>
        {
            new()
            {
                StrikePrice = 25000m,
                PutOiChange = 600000m,
                CallOiChange = 50000m,
                CallBuildup = OiBuildupType.LongBuildup,
                PutBuildup = OiBuildupType.ShortBuildup // Put writing
            },
            new()
            {
                StrikePrice = 25050m,
                PutOiChange = 400000m,
                CallOiChange = 80000m,
                CallBuildup = OiBuildupType.LongBuildup,
                PutBuildup = OiBuildupType.ShortBuildup
            }
        };

        var score = _service.CalculateSentimentScore(pcr, levels, strikes, spot);

        score.Should().BeGreaterThan(40); // Strongly bullish score
    }
}
