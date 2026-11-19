using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Infrastructure;
using NiftyQuant.Infrastructure.Persistence;
using Xunit;

namespace NiftyQuant.IntegrationTests;

public class OptionChainSnapshotIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly NiftyQuantDbContext _dbContext;

    public OptionChainSnapshotIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets("niftyquant-api-secrets-2026")
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _snapshotEngine = _serviceProvider.GetRequiredService<IOptionChainSnapshotEngine>();
        _dbContext = _serviceProvider.GetRequiredService<NiftyQuantDbContext>();
    }

    [Fact]
    public async Task CaptureAndPersistSnapshotAsync_ShouldSaveSnapshotAndQuotesToSqlServer()
    {
        // Act: Capture live snapshot from Upstox and persist to SQL Server
        var snapshot = await _snapshotEngine.CaptureAndPersistSnapshotAsync("NSE_INDEX|Nifty 50");

        // Assert: In-memory snapshot validation
        snapshot.Should().NotBeNull();
        snapshot!.UnderlyingKey.Should().Be("NSE_INDEX|Nifty 50");
        snapshot.UnderlyingPrice.Should().BeGreaterThan(15000m);
        snapshot.TotalCallOi.Should().BeGreaterThan(0m);
        snapshot.TotalPutOi.Should().BeGreaterThan(0m);
        snapshot.Pcr.Should().BeGreaterThan(0m);
        snapshot.MaxPainStrike.Should().BeGreaterThan(15000m);
        snapshot.HighestCallOiStrike.Should().BeGreaterThan(15000m);
        snapshot.HighestPutOiStrike.Should().BeGreaterThan(15000m);
        snapshot.Quotes.Should().NotBeEmpty();

        // Assert: Database persistence check
        var dbSnapshot = await _dbContext.OptionChainSnapshots
            .Include(s => s.Quotes)
            .OrderByDescending(s => s.SnapshotTimeUtc)
            .FirstOrDefaultAsync(s => s.Id == snapshot.Id);

        dbSnapshot.Should().NotBeNull();
        dbSnapshot!.Pcr.Should().Be(snapshot.Pcr);
        dbSnapshot.Quotes.Should().NotBeEmpty();

        // Verify latest snapshot query API
        var latest = await _snapshotEngine.GetLatestSnapshotAsync("NSE_INDEX|Nifty 50");
        latest.Should().NotBeNull();
        latest!.Id.Should().BeGreaterThanOrEqualTo(snapshot.Id);
    }
}
