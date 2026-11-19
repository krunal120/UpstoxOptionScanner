using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Infrastructure;
using Xunit;

namespace NiftyQuant.IntegrationTests;

public class UpstoxClientIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUpstoxClient _upstoxClient;

    public UpstoxClientIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets("niftyquant-api-secrets-2026")
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _upstoxClient = _serviceProvider.GetRequiredService<IUpstoxClient>();
    }

    [Fact]
    public async Task GetMarketQuoteAsync_Nifty50_ShouldReturnLiveQuote()
    {
        // Act
        var quote = await _upstoxClient.GetMarketQuoteAsync("NSE_INDEX|Nifty 50");

        // Assert
        quote.Should().NotBeNull();
        quote!.LastPrice.Should().BeGreaterThan(10000m);
        quote.Ohlc.Should().NotBeNull();
        quote.Ohlc!.High.Should().BeGreaterThanOrEqualTo(quote.Ohlc.Low);
    }

    [Fact]
    public async Task GetOptionContractsAsync_Nifty50_ShouldReturnContracts()
    {
        // Act
        var contracts = await _upstoxClient.GetOptionContractsAsync("NSE_INDEX|Nifty 50");

        // Assert
        contracts.Should().NotBeNullOrEmpty();
        contracts.Should().Contain(c => c.LotSize > 0);
        contracts.Select(c => c.Expiry).Distinct().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetOptionChainAsync_ShouldReturnStrikesAndGreeks()
    {
        // Arrange
        var contracts = await _upstoxClient.GetOptionContractsAsync("NSE_INDEX|Nifty 50");
        var nearestExpiry = contracts.Select(c => c.Expiry).Distinct().OrderBy(e => e).First();

        // Act
        var chain = await _upstoxClient.GetOptionChainAsync("NSE_INDEX|Nifty 50", nearestExpiry);

        // Assert
        chain.Should().NotBeNullOrEmpty();
        var sampleStrike = chain.FirstOrDefault(c => c.CallOptions?.MarketData != null && c.PutOptions?.MarketData != null);
        sampleStrike.Should().NotBeNull();
        sampleStrike!.UnderlyingSpotPrice.Should().BeGreaterThan(10000m);
    }

    [Fact]
    public async Task GetHistoricalCandlesAsync_ShouldReturnChronologicalCandles()
    {
        // Arrange
        var toDate = DateTime.Now.ToString("yyyy-MM-dd");
        var fromDate = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd");

        // Act
        var candles = await _upstoxClient.GetHistoricalCandlesAsync("NSE_INDEX|Nifty 50", "minutes", 1, toDate, fromDate);

        // Assert
        candles.Should().NotBeNullOrEmpty();
        candles.First().Open.Should().BeGreaterThan(0m);
        candles.Should().BeInAscendingOrder(c => c.Timestamp);
    }
}
