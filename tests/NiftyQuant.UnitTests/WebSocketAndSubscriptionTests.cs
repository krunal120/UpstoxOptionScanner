using Com.Upstox.Marketdatafeederv3Udapi.Rpc.Proto;
using FluentAssertions;
using NiftyQuant.Infrastructure.WebSockets;
using Xunit;

namespace NiftyQuant.UnitTests;

public class WebSocketAndSubscriptionTests
{
    [Theory]
    [InlineData(25334.25, 25350.0)]
    [InlineData(25310.0, 25300.0)]
    [InlineData(25375.0, 25400.0)]
    [InlineData(25000.0, 25000.0)]
    [InlineData(25024.9, 25000.0)]
    [InlineData(25025.1, 25050.0)]
    public void CalculateAtmStrike_ShouldRoundToNearestFifty(decimal spotPrice, decimal expectedAtm)
    {
        // Act
        var atm = SubscriptionManager.CalculateAtmStrike(spotPrice);

        // Assert
        atm.Should().Be(expectedAtm);
    }

    [Fact]
    public void ParseTick_FromMarketFullFeed_ShouldMapAllFieldsCorrectly()
    {
        // Arrange
        var feed = new Feed
        {
            FullFeed = new FullFeed
            {
                MarketFF = new MarketFullFeed
                {
                    Ltpc = new LTPC
                    {
                        Ltp = 150.25,
                        Ltq = 65,
                        Cp = 140.0,
                        Ltt = 1795068785000
                    },
                    Vtt = 125000,
                    Oi = 450000.0,
                    Iv = 14.5,
                    OptionGreeks = new OptionGreeks
                    {
                        Delta = 0.52,
                        Gamma = 0.0012,
                        Theta = -15.4,
                        Vega = 8.5
                    }
                }
            }
        };

        feed.FullFeed.MarketFF.MarketLevel = new MarketLevel();
        feed.FullFeed.MarketFF.MarketLevel.BidAskQuote.Add(new Quote
        {
            BidP = 150.0,
            BidQ = 650,
            AskP = 150.5,
            AskQ = 1300
        });

        // Act
        var tick = UpstoxWebSocketClient.ParseTick("NSE_FO|64116", feed);

        // Assert
        tick.Should().NotBeNull();
        tick!.InstrumentKey.Should().Be("NSE_FO|64116");
        tick.Ltp.Should().Be(150.25m);
        tick.LastTradedQuantity.Should().Be(65);
        tick.Volume.Should().Be(125000);
        tick.OpenInterest.Should().Be(450000m);
        tick.ImpliedVolatility.Should().Be(14.5m);
        tick.Delta.Should().Be(0.52m);
        tick.Gamma.Should().Be(0.0012m);
        tick.Theta.Should().Be(-15.4m);
        tick.Vega.Should().Be(8.5m);
        tick.BidPrice.Should().Be(150.0m);
        tick.BidQuantity.Should().Be(650);
        tick.AskPrice.Should().Be(150.5m);
        tick.AskQuantity.Should().Be(1300);
    }

    [Fact]
    public void ParseTick_FromIndexFullFeed_ShouldMapIndexFields()
    {
        // Arrange
        var feed = new Feed
        {
            FullFeed = new FullFeed
            {
                IndexFF = new IndexFullFeed
                {
                    Ltpc = new LTPC
                    {
                        Ltp = 25334.25,
                        Cp = 25227.95
                    }
                }
            }
        };

        // Act
        var tick = UpstoxWebSocketClient.ParseTick("NSE_INDEX|Nifty 50", feed);

        // Assert
        tick.Should().NotBeNull();
        tick!.IsIndex.Should().BeTrue();
        tick.Ltp.Should().Be(25334.25m);
        tick.ClosePrice.Should().Be(25227.95m);
    }
}
