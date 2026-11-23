using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure;
using Xunit;

namespace NiftyQuant.IntegrationTests;

public class RiskManagementIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRiskManagementService _riskService;

    public RiskManagementIntegrationTests()
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
        _riskService = _serviceProvider.GetRequiredService<IRiskManagementService>();
    }

    [Fact]
    public async Task GetOrCreateDefaultAccountAsync_LoadsOrSeedsPaperAccountInSqlServer()
    {
        // Act
        var account = await _riskService.GetOrCreateDefaultAccountAsync();

        // Assert
        account.Should().NotBeNull();
        account.InitialCapital.Should().Be(50000m);
        account.AvailableBalance.Should().BeGreaterThan(0m);
        account.DailyLossLimit.Should().Be(3000m);
        account.MaxRiskPerTrade.Should().Be(1500m);
    }

    [Fact]
    public async Task GetDailyRiskStatusAsync_ReturnsLiveDatabaseRiskState()
    {
        // Act
        var status = await _riskService.GetDailyRiskStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status.DailyLossLimit.Should().Be(3000m);
        status.MaxDailyTradesLimit.Should().Be(6);
        status.MaxConsecutiveLossesLimit.Should().Be(2);
        status.MaxConcurrentPositionsLimit.Should().Be(1);
    }

    [Fact]
    public async Task ValidatePreTradeAsync_WithCompliantSignal_EvaluatesHardRiskRules()
    {
        // Arrange: In-session time (10:30 AM IST = 05:00 UTC)
        var signalTime = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 5, 0, 0, DateTimeKind.Utc);
        var signal = new StrategySignal
        {
            Id = Guid.NewGuid(),
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|NIFTY26NOV24000CE",
            EntryPremium = 135.0m,
            StopLossPremium = 115.0m, // Risk = 20
            Target1Premium = 175.0m,   // Reward = 40 -> 1:2.0
            SignalTimeUtc = signalTime
        };

        // Act
        var result = await _riskService.ValidatePreTradeAsync(signal);

        // Assert
        result.Should().NotBeNull();
        result.ExecutionMode.Should().Be("SimulatedPaperTradingOnly");
        result.LotSize.Should().Be(65);

        // If no active open positions are currently blocking in the DB, it should be approved
        if (result.IsApproved)
        {
            result.AllocatedLots.Should().Be(1);
            result.AllocatedQuantity.Should().Be(65);
            result.RewardToRiskRatio.Should().Be(2.0m);
        }
        else
        {
            result.VetoReasons.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task ResetDailyLimitsAsync_SuccessfullyExecutesAgainstDatabase()
    {
        // Act
        await _riskService.ResetDailyLimitsAsync();
        var account = await _riskService.GetOrCreateDefaultAccountAsync();

        // Assert
        account.IsTradingLockedForDay.Should().BeFalse();
    }
}
