using FluentAssertions;
using NiftyQuant.Domain.Entities;
using Xunit;

namespace NiftyQuant.UnitTests;

public class IndexRegistryTests
{
    [Theory]
    [InlineData("NSE_INDEX|Nifty 50", "NIFTY", 65, 50.0, 35.0, 55.0)]
    [InlineData("NSE_INDEX:Nifty 50", "NIFTY", 65, 50.0, 35.0, 55.0)]
    [InlineData("NIFTY", "NIFTY", 65, 50.0, 35.0, 55.0)]
    [InlineData("NSE_INDEX|Nifty Bank", "BANKNIFTY", 30, 100.0, 85.0, 140.0)]
    [InlineData("NSE_INDEX:Nifty Bank", "BANKNIFTY", 30, 100.0, 85.0, 140.0)]
    [InlineData("BANKNIFTY", "BANKNIFTY", 30, 100.0, 85.0, 140.0)]
    [InlineData("BSE_INDEX|SENSEX", "SENSEX", 20, 100.0, 140.0, 220.0)]
    [InlineData("BSE_INDEX:SENSEX", "SENSEX", 20, 100.0, 140.0, 220.0)]
    [InlineData("SENSEX", "SENSEX", 20, 100.0, 140.0, 220.0)]
    public void GetProfile_SupportedKeys_ReturnsExpectedProfile(
        string key, string expectedSymbol, int expectedLot, double expectedStrike, double expectedPullback, double expectedOverextended)
    {
        var profile = IndexRegistry.GetProfile(key);

        profile.Should().NotBeNull();
        profile.Symbol.Should().Be(expectedSymbol);
        profile.LotSize.Should().Be(expectedLot);
        profile.StrikeInterval.Should().Be((decimal)expectedStrike);
        profile.VwapPullbackPoints.Should().Be((decimal)expectedPullback);
        profile.VwapOverExtendedPoints.Should().Be((decimal)expectedOverextended);
    }

    [Fact]
    public void SupportedIndices_ContainsAllThreeMajorIndices()
    {
        var indices = IndexRegistry.SupportedIndices;

        indices.Should().HaveCount(3);
        indices.Should().Contain(p => p.Symbol == "NIFTY");
        indices.Should().Contain(p => p.Symbol == "BANKNIFTY");
        indices.Should().Contain(p => p.Symbol == "SENSEX");
    }

    [Theory]
    [InlineData("NSE_INDEX|Nifty 50", true)]
    [InlineData("NSE_INDEX|Nifty Bank", true)]
    [InlineData("BSE_INDEX|SENSEX", true)]
    [InlineData("NSE_INDEX|RELIANCE", false)]
    public void IsSupportedIndex_TestsExpectedMembership(string key, bool expected)
    {
        IndexRegistry.IsSupportedIndex(key).Should().Be(expected);
    }
}
