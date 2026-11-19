using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Models.Risk;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Application.Services.Risk;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;
using NiftyQuant.Infrastructure.Services;
using Xunit;

namespace NiftyQuant.UnitTests;

public class RiskManagementServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly RiskManagementService _riskService;
    private readonly IOptions<RiskOptions> _riskOptions;
    private readonly PositionSizingCalculator _calculator;

    public RiskManagementServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<NiftyQuantDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        _riskOptions = Options.Create(new RiskOptions
        {
            InitialCapital = 50000m,
            RiskPerTradePercent = 0.5m, // Rs 250
            MaxDailyLossPercent = 1.5m, // Rs 750
            MinRewardRiskRatio = 2.0m,
            MaxConcurrentPositions = 1,
            MaxDailyTrades = 6,
            MaxConsecutiveLosses = 2
        });

        _calculator = new PositionSizingCalculator();

        services.AddSingleton(_riskOptions);
        services.AddSingleton(_calculator);
        services.AddSingleton<RiskManagementService>(sp =>
            new RiskManagementService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                _calculator,
                _riskOptions,
                NullLogger<RiskManagementService>.Instance));

        _serviceProvider = services.BuildServiceProvider();
        _riskService = _serviceProvider.GetRequiredService<RiskManagementService>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task ValidatePreTradeAsync_WithValidSignalDuringMarketHours_ApprovesTrade()
    {
        // Arrange: 10:30 AM IST = 05:00 UTC
        var signalTime = new DateTime(2026, 11, 19, 5, 0, 0, DateTimeKind.Utc);
        var signal = new StrategySignal
        {
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            EntryPremium = 120.0m,
            StopLossPremium = 100.0m, // Risk = 20
            Target1Premium = 160.0m,   // Reward = 40 -> 1:2.0
            SignalTimeUtc = signalTime
        };

        // Act
        var result = await _riskService.ValidatePreTradeAsync(signal);

        // Assert
        result.IsApproved.Should().BeTrue();
        result.AllocatedLots.Should().Be(1);
        result.AllocatedQuantity.Should().Be(65);
        result.RequiredMargin.Should().Be(120m * 65);
        result.ExpectedRiskAmount.Should().Be(20m * 65);
        result.RewardToRiskRatio.Should().Be(2.0m);
        result.VetoReasons.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidatePreTradeAsync_OutsideMarketHours_VetoesTrade()
    {
        // Arrange: 08:30 AM IST = 03:00 UTC (Pre-market)
        var preMarketTime = new DateTime(2026, 11, 19, 3, 0, 0, DateTimeKind.Utc);
        var signal = new StrategySignal
        {
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            EntryPremium = 120.0m,
            StopLossPremium = 100.0m,
            Target1Premium = 160.0m,
            SignalTimeUtc = preMarketTime
        };

        // Act
        var result = await _riskService.ValidatePreTradeAsync(signal);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.VetoReasons.Should().Contain(v => v.Contains("Time-of-day veto"));
    }

    [Fact]
    public async Task ValidatePreTradeAsync_WhenRewardRiskIsBelowMinimum_VetoesTrade()
    {
        // Arrange: 10:30 AM IST
        var signalTime = new DateTime(2026, 11, 19, 5, 0, 0, DateTimeKind.Utc);
        var signal = new StrategySignal
        {
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            EntryPremium = 100.0m,
            StopLossPremium = 80.0m,  // Risk = 20
            Target1Premium = 125.0m,  // Reward = 25 -> 1:1.25 (Below 1:2 min)
            SignalTimeUtc = signalTime
        };

        // Act
        var result = await _riskService.ValidatePreTradeAsync(signal);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.VetoReasons.Should().Contain(v => v.Contains("Reward-to-risk ratio"));
    }

    [Fact]
    public async Task ValidatePreTradeAsync_WhenActivePositionExists_VetoesTrade()
    {
        // Arrange: Insert existing open position
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(new PaperPosition
            {
                InstrumentKey = "NSE_FO|NIFTY26NOV24000CE",
                IsOpen = true,
                Quantity = 65,
                AverageBuyPrice = 110m
            });
            await db.SaveChangesAsync();
        }

        var signalTime = new DateTime(2026, 11, 19, 5, 0, 0, DateTimeKind.Utc);
        var signal = new StrategySignal
        {
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            EntryPremium = 120.0m,
            StopLossPremium = 100.0m,
            Target1Premium = 160.0m,
            SignalTimeUtc = signalTime
        };

        // Act
        var result = await _riskService.ValidatePreTradeAsync(signal);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.VetoReasons.Should().Contain(v => v.Contains("Maximum concurrent positions limit"));
    }

    [Fact]
    public async Task ValidatePreTradeAsync_WhenAveragingDownOnExistingContract_VetoesTrade()
    {
        // Arrange: Insert existing open position on the exact same instrument key
        const string contractKey = "NSE_FO|NIFTY26NOV24100CE";
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(new PaperPosition
            {
                InstrumentKey = contractKey,
                IsOpen = true,
                Quantity = 65,
                AverageBuyPrice = 130m,
                CurrentLtp = 115m // In a loss
            });
            await db.SaveChangesAsync();
        }

        var signalTime = new DateTime(2026, 11, 19, 5, 0, 0, DateTimeKind.Utc);
        var signal = new StrategySignal
        {
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = contractKey,
            EntryPremium = 115.0m,
            StopLossPremium = 95.0m,
            Target1Premium = 155.0m,
            SignalTimeUtc = signalTime
        };

        // Act
        var result = await _riskService.ValidatePreTradeAsync(signal);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.VetoReasons.Should().Contain(v => v.Contains("Cannot add to or average down"));
    }

    [Fact]
    public async Task ValidatePreTradeAsync_WhenConsecutiveLossLimitReached_VetoesTrade()
    {
        // Arrange: Insert 2 consecutive loss trades today
        var todayUtc = DateTime.UtcNow.Date.AddHours(4); // Today morning in UTC
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperTrades.AddRange(
                new PaperTrade { NetPnL = -300m, ExitTimeUtc = todayUtc.AddMinutes(10), InstrumentKey = "C1" },
                new PaperTrade { NetPnL = -250m, ExitTimeUtc = todayUtc.AddMinutes(30), InstrumentKey = "C2" }
            );
            await db.SaveChangesAsync();
        }

        var signalTime = new DateTime(2026, 11, 19, 5, 0, 0, DateTimeKind.Utc);
        var signal = new StrategySignal
        {
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            EntryPremium = 120.0m,
            StopLossPremium = 100.0m,
            Target1Premium = 160.0m,
            SignalTimeUtc = signalTime
        };

        // Act
        var result = await _riskService.ValidatePreTradeAsync(signal);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.VetoReasons.Should().Contain(v => v.Contains("Consecutive loss"));
    }

    [Fact]
    public async Task GetDailyRiskStatusAsync_CalculatesLimitsAndStatusCorrectly()
    {
        // Act
        var status = await _riskService.GetDailyRiskStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status.InitialCapital.Should().Be(50000m);
        status.AvailableBalance.Should().Be(50000m);
        status.DailyLossLimit.Should().Be(750m);
        status.DailyLossRemaining.Should().Be(750m);
        status.TradesCountToday.Should().Be(0);
        status.MaxDailyTradesLimit.Should().Be(6);
        status.MaxConsecutiveLossesLimit.Should().Be(2);
        status.IsCircuitBreakerTripped.Should().BeFalse();
        status.CanOpenNewPositions.Should().BeTrue();
    }

    [Fact]
    public async Task RecordTradeResult_WhenLossExceedsDailyLimit_LocksAccount()
    {
        // Arrange: Huge loss of 800 (exceeds 750 limit)
        var trade = new PaperTrade
        {
            InstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            GrossPnL = -750m,
            TotalCharges = 50m,
            NetPnL = -800m,
            ExitTimeUtc = DateTime.UtcNow
        };

        // Act
        await _riskService.RecordTradeResultAsync(trade);
        var status = await _riskService.GetDailyRiskStatusAsync();

        // Assert
        status.IsCircuitBreakerTripped.Should().BeTrue();
        status.CircuitBreakerReason.Should().Contain("Maximum daily loss limit reached");
        status.CanOpenNewPositions.Should().BeFalse();
    }

    [Fact]
    public async Task ResetDailyLimitsAsync_UnlocksAccountSuccessfully()
    {
        // Ensure account is initialized in memory first
        var account = await _riskService.GetOrCreateDefaultAccountAsync();

        // Arrange: Lock account
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var trackedAccount = await db.PaperAccounts.FirstAsync();
            trackedAccount.IsTradingLockedForDay = true;
            await db.SaveChangesAsync();
        }

        // Act
        await _riskService.ResetDailyLimitsAsync();
        var accountAfter = await _riskService.GetOrCreateDefaultAccountAsync();

        // Assert
        accountAfter.IsTradingLockedForDay.Should().BeFalse();
    }
}
