using FluentAssertions;
using NiftyQuant.Application.Common.Models.Upstox;
using NiftyQuant.Infrastructure.Services;
using Xunit;

namespace NiftyQuant.UnitTests;

public class MaxPainCalculatorTests
{
    [Fact]
    public void CalculateMaxPain_EmptyChain_ReturnsZero()
    {
        var result = OptionChainSnapshotEngine.CalculateMaxPain(Array.Empty<OptionChainItemDto>());
        result.Should().Be(0m);
    }

    [Fact]
    public void CalculateMaxPain_SingleStrike_ReturnsThatStrike()
    {
        var chain = new List<OptionChainItemDto>
        {
            new()
            {
                StrikePrice = 24500m,
                CallOptions = new OptionDetailsDto
                {
                    MarketData = new OptionMarketDataDto { OpenInterest = 1000 }
                },
                PutOptions = new OptionDetailsDto
                {
                    MarketData = new OptionMarketDataDto { OpenInterest = 1000 }
                }
            }
        };

        var result = OptionChainSnapshotEngine.CalculateMaxPain(chain);
        result.Should().Be(24500m);
    }

    [Fact]
    public void CalculateMaxPain_KnownDistribution_ReturnsMinimumLossStrike()
    {
        // Setup strikes: 24000, 24100, 24200, 24300
        // Total Option Buyer Payout calculation:
        // Strike 24000: CE OI = 10,000, PE OI = 50,000
        // Strike 24100: CE OI = 20,000, PE OI = 30,000
        // Strike 24200: CE OI = 60,000, PE OI = 15,000
        // Strike 24300: CE OI = 80,000, PE OI = 5,000
        //
        // Loss at 24000: Put loss = 100*30000 + 200*15000 + 300*5000 = 3,000,000 + 3,000,000 + 1,500,000 = 7,500,000
        // Loss at 24100: Call loss = 100*10000 = 1,000,000; Put loss = 100*15000 + 200*5000 = 2,500,000 -> Total = 3,500,000
        // Loss at 24200: Call loss = 200*10000 + 100*20000 = 4,000,000; Put loss = 100*5000 = 500,000 -> Total = 4,500,000
        // Loss at 24300: Call loss = 300*10000 + 200*20000 + 100*60000 = 13,000,000; Put loss = 0 -> Total = 13,000,000
        // Minimum loss is 3,500,000 at Strike 24100!
        var chain = new List<OptionChainItemDto>
        {
            new()
            {
                StrikePrice = 24000m,
                CallOptions = new OptionDetailsDto { MarketData = new OptionMarketDataDto { OpenInterest = 10000m } },
                PutOptions = new OptionDetailsDto { MarketData = new OptionMarketDataDto { OpenInterest = 50000m } }
            },
            new()
            {
                StrikePrice = 24100m,
                CallOptions = new OptionDetailsDto { MarketData = new OptionMarketDataDto { OpenInterest = 20000m } },
                PutOptions = new OptionDetailsDto { MarketData = new OptionMarketDataDto { OpenInterest = 30000m } }
            },
            new()
            {
                StrikePrice = 24200m,
                CallOptions = new OptionDetailsDto { MarketData = new OptionMarketDataDto { OpenInterest = 60000m } },
                PutOptions = new OptionDetailsDto { MarketData = new OptionMarketDataDto { OpenInterest = 15000m } }
            },
            new()
            {
                StrikePrice = 24300m,
                CallOptions = new OptionDetailsDto { MarketData = new OptionMarketDataDto { OpenInterest = 80000m } },
                PutOptions = new OptionDetailsDto { MarketData = new OptionMarketDataDto { OpenInterest = 5000m } }
            }
        };

        var maxPain = OptionChainSnapshotEngine.CalculateMaxPain(chain);
        maxPain.Should().Be(24100m);
    }
}
