using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure;
using Xunit;

namespace NiftyQuant.IntegrationTests;

public class OptionsIntelligenceIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsIntelligenceService _optionsIntelligence;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;

    public OptionsIntelligenceIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets("niftyquant-api-secrets-2026")
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _optionsIntelligence = _serviceProvider.GetRequiredService<IOptionsIntelligenceService>();
        _snapshotEngine = _serviceProvider.GetRequiredService<IOptionChainSnapshotEngine>();
    }

    [Fact]
    public async Task AnalyzeSnapshot_WithLiveOptionChain_ProducesComprehensiveIntelligenceReport()
    {
        // Arrange: Capture live snapshot from Upstox
        const string key = "NSE_INDEX|Nifty 50";
        var snapshot = await _snapshotEngine.CaptureAndPersistSnapshotAsync(key);
        snapshot.Should().NotBeNull();
        snapshot!.Quotes.Should().NotBeEmpty();

        // Act: Run Options Intelligence Engine
        var report = _optionsIntelligence.AnalyzeSnapshot(snapshot);

        // Assert
        report.Should().NotBeNull();
        report.UnderlyingKey.Should().Be(key);
        report.UnderlyingPrice.Should().BeGreaterThan(15000m);
        report.TotalStrikesAnalyzed.Should().BeGreaterThan(10);

        // PCR Analysis
        report.PcrAnalysis.Should().NotBeNull();
        report.PcrAnalysis.PcrOi.Should().BeGreaterThan(0m);
        report.PcrAnalysis.SentimentZone.Should().NotBeNullOrWhiteSpace();

        // Key Levels
        report.KeyLevels.Should().NotBeNull();
        report.KeyLevels.MaxPainStrike.Should().BeGreaterThan(15000m);
        report.KeyLevels.MajorCallResistance.Should().BeGreaterThan(15000m);
        report.KeyLevels.MajorPutSupport.Should().BeGreaterThan(15000m);

        // Sentiment Score
        report.OptionsSentimentScore.Should().BeInRange(-100, 100);
        report.SentimentSummary.Should().NotBeNullOrWhiteSpace();

        // Contract Recommendations
        report.RecommendedCe.Should().NotBeNull();
        report.RecommendedCe!.OptionType.Should().Be(OptionType.CE);
        report.RecommendedCe.Ltp.Should().BeGreaterThan(0m);
        report.RecommendedCe.LiquidityScore.Should().BeGreaterThan(0m);

        report.RecommendedPe.Should().NotBeNull();
        report.RecommendedPe!.OptionType.Should().Be(OptionType.PE);
        report.RecommendedPe.Ltp.Should().BeGreaterThan(0m);
        report.RecommendedPe.LiquidityScore.Should().BeGreaterThan(0m);
    }
}
