using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Risk;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Application.Services.Risk;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;
using NiftyQuant.Infrastructure.Services;
using Xunit;

namespace NiftyQuant.UnitTests;

public class PaperTradingEngineTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PaperTradingEngine _paperEngine;
    private readonly Mock<IRiskManagementService> _riskServiceMock;
    private readonly PositionSizingCalculator _calculator;
    private readonly IOptions<RiskOptions> _riskOptions;

    public PaperTradingEngineTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<NiftyQuantDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        _riskOptions = Options.Create(new RiskOptions
        {
            InitialCapital = 50000m,
            RiskPerTradePercent = 0.5m,
            MaxDailyLossPercent = 1.5m,
            MinRewardRiskRatio = 2.0m,
            MaxConcurrentPositions = 1,
            SlippagePerOrderPercent = 0.2m, // 0.2%
            BrokeragePerOrder = 20.0m,
            EnableTrailingStopLoss = true
        });

        _calculator = new PositionSizingCalculator();
        _riskServiceMock = new Mock<IRiskManagementService>();

        services.AddSingleton(_riskOptions);
        services.AddSingleton(_calculator);
        services.AddSingleton(_riskServiceMock.Object);

        services.AddSingleton<PaperTradingEngine>(sp =>
            new PaperTradingEngine(
                sp.GetRequiredService<IServiceScopeFactory>(),
                _riskServiceMock.Object,
                _calculator,
                _riskOptions,
                NullLogger<PaperTradingEngine>.Instance));

        _serviceProvider = services.BuildServiceProvider();
        _paperEngine = _serviceProvider.GetRequiredService<PaperTradingEngine>();

        // Seed initial paper account
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
        db.PaperAccounts.Add(new PaperAccount
        {
            InitialCapital = 50000m,
            AvailableBalance = 50000m,
            UtilizedMargin = 0m,
            DailyLossLimit = 750m,
            MaxRiskPerTrade = 250m
        });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task ExecuteSignalAsync_WhenRiskApproved_CreatesFilledOrderAndOpenPositionWithSlippage()
    {
        // Arrange
        var signal = new StrategySignal
        {
            Id = Guid.NewGuid(),
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            SelectedOptionType = OptionType.CE,
            SelectedStrike = 24100m,
            SelectedExpiry = new DateTime(2026, 11, 26),
            EntryPremium = 120.0m,
            StopLossPremium = 100.0m,
            Target1Premium = 160.0m,
            SignalTimeUtc = DateTime.UtcNow
        };

        _riskServiceMock.Setup(r => r.ValidatePreTradeAsync(signal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreTradeRiskValidationResult
            {
                IsApproved = true,
                AllocatedLots = 1,
                AllocatedQuantity = 65,
                RequiredMargin = 7800m,
                ExpectedRiskAmount = 1300m
            });

        PaperOrder? placedOrder = null;
        PaperPosition? openedPos = null;
        _paperEngine.OnOrderPlaced += o => { placedOrder = o; return Task.CompletedTask; };
        _paperEngine.OnPositionOpened += p => { openedPos = p; return Task.CompletedTask; };

        // Act
        var result = await _paperEngine.ExecuteSignalAsync(signal, simulateSlippage: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(PaperOrderStatus.Filled);
        result.Quantity.Should().Be(65);
        result.OrderedPrice.Should().Be(120.0m);

        // Slippage: 0.2% on 120 = 0.24 -> executedPrice = 120.24
        result.Slippage.Should().Be(0.24m);
        result.ExecutedPrice.Should().Be(120.24m);
        result.PositionId.Should().NotBeNull();

        placedOrder.Should().NotBeNull();
        openedPos.Should().NotBeNull();
        openedPos!.Quantity.Should().Be(65);
        openedPos.AverageBuyPrice.Should().Be(120.24m);
        openedPos.IsOpen.Should().BeTrue();

        // Verify account balance updated
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
        var account = await db.PaperAccounts.FirstAsync();
        decimal expectedMargin = 120.24m * 65;
        account.UtilizedMargin.Should().Be(expectedMargin);
        account.AvailableBalance.Should().Be(50000m - expectedMargin);
    }

    [Fact]
    public async Task ExecuteSignalAsync_WhenRiskVetoed_RecordsRejectedOrderAndDoesNotOpenPosition()
    {
        // Arrange
        var signal = new StrategySignal
        {
            Id = Guid.NewGuid(),
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            EntryPremium = 120.0m,
            StopLossPremium = 100.0m
        };

        _riskServiceMock.Setup(r => r.ValidatePreTradeAsync(signal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreTradeRiskValidationResult
            {
                IsApproved = false,
                VetoReasons = new List<string> { "Maximum daily trades limit reached" }
            });

        // Act
        var result = await _paperEngine.ExecuteSignalAsync(signal);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(PaperOrderStatus.Rejected);
        result.PositionId.Should().BeNull();
        result.Message.Should().Contain("Maximum daily trades limit reached");

        // Verify no open position in database
        var openPositions = await _paperEngine.GetOpenPositionsAsync();
        openPositions.Should().BeEmpty();
    }

    [Fact]
    public async Task ClosePositionAsync_CalculatesGrossAndNetPnLAndRestoresMargin()
    {
        // Arrange: Insert an open position
        Guid posId = Guid.NewGuid();
        var position = new PaperPosition
        {
            Id = posId,
            InstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            OptionType = OptionType.CE,
            Quantity = 65,
            AverageBuyPrice = 120.0m,
            CurrentLtp = 150.0m,
            StopLossPrice = 100.0m,
            TargetPrice = 160.0m,
            IsOpen = true,
            OpenedAtUtc = DateTime.UtcNow.AddMinutes(-30)
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(position);

            var account = await db.PaperAccounts.FirstAsync();
            account.UtilizedMargin = 120m * 65;
            account.AvailableBalance = 50000m - (120m * 65);
            await db.SaveChangesAsync();
        }

        PaperTrade? closedTrade = null;
        _paperEngine.OnPositionClosed += t => { closedTrade = t; return Task.CompletedTask; };

        // Act: Close position at 150
        var result = await _paperEngine.ClosePositionAsync(posId, exitReason: "ManualClose", exitPrice: 150.0m);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.PositionId.Should().Be(posId);
        result.Quantity.Should().Be(65);

        // Selling slippage: 0.2% on 150 = 0.30 -> exit price = 149.70
        result.ExitPrice.Should().Be(149.70m);

        // Gross PnL: (149.70 - 120.00) * 65 = 29.70 * 65 = 1930.50
        result.GrossPnL.Should().Be(1930.50m);
        result.TotalCharges.Should().BeGreaterThan(40m);
        result.NetPnL.Should().Be(result.GrossPnL - result.TotalCharges);

        closedTrade.Should().NotBeNull();
        closedTrade!.ExitReason.Should().Be("ManualClose");

        // Verify account margin restored
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var account = await db.PaperAccounts.FirstAsync();
            account.UtilizedMargin.Should().Be(0m);
            account.AvailableBalance.Should().Be(50000m);
        }
    }

    [Fact]
    public async Task UpdatePositionPricesAsync_WhenStopLossHit_AutoClosesPosition()
    {
        // Arrange: Position with Buy=120, SL=100
        Guid posId = Guid.NewGuid();
        var position = new PaperPosition
        {
            Id = posId,
            InstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            OptionType = OptionType.CE,
            Quantity = 65,
            AverageBuyPrice = 120.0m,
            CurrentLtp = 110.0m,
            StopLossPrice = 100.0m,
            TargetPrice = 160.0m,
            IsOpen = true
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(position);
            await db.SaveChangesAsync();
        }

        // Act: Market tick drops to 98 (Breaches SL 100)
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26NOV24100CE", 98.0m);

        // Assert: Position should be closed
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var pos = await db.PaperPositions.FindAsync(posId);
            pos!.IsOpen.Should().BeFalse();

            var trade = await db.PaperTrades.FirstOrDefaultAsync(t => t.InstrumentKey == "NSE_FO|NIFTY26NOV24100CE");
            trade.Should().NotBeNull();
            trade!.ExitReason.Should().Be("StopLossHit");
        }
    }

    [Fact]
    public async Task UpdatePositionPricesAsync_WhenTargetHit_AutoClosesPosition()
    {
        // Arrange: Position with Buy=120, Target=160
        Guid posId = Guid.NewGuid();
        var position = new PaperPosition
        {
            Id = posId,
            InstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            OptionType = OptionType.CE,
            Quantity = 65,
            AverageBuyPrice = 120.0m,
            CurrentLtp = 140.0m,
            StopLossPrice = 100.0m,
            TargetPrice = 160.0m,
            IsOpen = true
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(position);
            await db.SaveChangesAsync();
        }

        // Act: Market tick rises to 162 (Hits Target 160)
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26NOV24100CE", 162.0m);

        // Assert: Position closed with TargetHit
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var pos = await db.PaperPositions.FindAsync(posId);
            pos!.IsOpen.Should().BeFalse();

            var trade = await db.PaperTrades.FirstOrDefaultAsync(t => t.InstrumentKey == "NSE_FO|NIFTY26NOV24100CE");
            trade.Should().NotBeNull();
            trade!.ExitReason.Should().Be("TargetHit");
        }
    }

    [Fact]
    public async Task UpdatePositionPricesAsync_When1Point5RReached_TrailsStopLossToBreakeven()
    {
        // Arrange: Buy=120, SL=100 -> Risk = 20 pts. 1.5R = 30 pts profit -> Price 150.
        Guid posId = Guid.NewGuid();
        var position = new PaperPosition
        {
            Id = posId,
            InstrumentKey = "NSE_FO|NIFTY26NOV24100CE",
            OptionType = OptionType.CE,
            Quantity = 65,
            AverageBuyPrice = 120.0m,
            CurrentLtp = 120.0m,
            StopLossPrice = 100.0m,
            TargetPrice = 180.0m,
            IsOpen = true
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(position);
            await db.SaveChangesAsync();
        }

        // Act: Price moves to 152 (Profit = 32 > 30 = 1.5R)
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26NOV24100CE", 152.0m);

        // Assert: Stop Loss trailed to protect profit (at least Breakeven + buffer)
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var pos = await db.PaperPositions.FindAsync(posId);
            pos!.IsOpen.Should().BeTrue();
            pos.StopLossPrice.Should().BeGreaterThanOrEqualTo(121.0m);
            pos.UnrealizedPnL.Should().Be((152m - 120m) * 65);
        }
    }

    [Fact]
    public async Task UpdatePositionPricesAsync_WhenProfitReaches6K_TrailsStopLossToLock75PercentOfGain()
    {
        var posId = Guid.NewGuid();
        var position = new PaperPosition
        {
            Id = posId,
            InstrumentKey = "NSE_FO|NIFTY26DEC25800CE",
            OptionType = OptionType.CE,
            Quantity = 65,
            AverageBuyPrice = 238.0m,
            CurrentLtp = 238.0m,
            StopLossPrice = 208.0m,
            TargetPrice = 400.0m,
            IsOpen = true
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(position);
            await db.SaveChangesAsync();
        }

        // Act: Price climbs to 338 (Gain = 100 pts, Total profit = 6500)
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26DEC25800CE", 338.0m);

        // Assert: Stop Loss ratcheted to lock >= 75% of gain (at least 238 + 75 = 313.0)
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var pos = await db.PaperPositions.FindAsync(posId);
            pos!.IsOpen.Should().BeTrue();
            pos.StopLossPrice.Should().BeGreaterThanOrEqualTo(313.0m);
            pos.MaxLtpReached.Should().Be(338.0m);
        }
    }

    [Fact]
    public async Task UpdatePositionPricesAsync_WhenPriceReversesAfter6KGain_AutoClosesWithTrailingStopHit()
    {
        var posId = Guid.NewGuid();
        var position = new PaperPosition
        {
            Id = posId,
            InstrumentKey = "NSE_FO|NIFTY26DEC25800CE",
            TradingSymbol = "NIFTY 01 DEC 25800 CE",
            OptionType = OptionType.CE,
            Quantity = 65,
            AverageBuyPrice = 238.0m,
            CurrentLtp = 238.0m,
            StopLossPrice = 208.0m,
            TargetPrice = 400.0m,
            IsOpen = true
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(position);
            await db.SaveChangesAsync();
        }

        // 1. Price climbs to 338 (locks in SL >= 313 via 75% gain lock & chandelier)
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26DEC25800CE", 338.0m);

        // 2. Price reverses from 338 down to 310 (below locked SL of 313)
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26DEC25800CE", 310.0m);

        // Assert: Position is closed with TrailingStopHit and handsome profit
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var pos = await db.PaperPositions.FindAsync(posId);
            pos!.IsOpen.Should().BeFalse();

            var trade = await db.PaperTrades.FirstOrDefaultAsync(t => t.InstrumentKey == "NSE_FO|NIFTY26DEC25800CE");
            trade.Should().NotBeNull();
            trade!.ExitReason.Should().Be("TrailingStopHit");
            trade.NetPnL.Should().BeGreaterThan(4000m); // Locked over ₹4,000 profit!
        }
    }

    [Fact]
    public async Task UpdatePositionPricesAsync_LowPremiumOption_TrailsPurelyOnPercentage()
    {
        // Scenario: Small premium (Rs. 50 option, capital = 50 * 65 = Rs. 3,250)
        // Static Rs. 1500 or 2500 would never trigger, but pure % triggers smoothly!
        var posId = Guid.NewGuid();
        var position = new PaperPosition
        {
            Id = posId,
            InstrumentKey = "NSE_FO|NIFTY26NOV25000CE",
            TradingSymbol = "NIFTY 23 NOV 25000 CE",
            OptionType = OptionType.CE,
            Quantity = 65,
            AverageBuyPrice = 50.0m,
            CurrentLtp = 50.0m,
            StopLossPrice = 40.0m, // 20% initial SL
            TargetPrice = 100.0m,
            IsOpen = true
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(position);
            await db.SaveChangesAsync();
        }

        // 1. Price climbs to 65 (+30% gain, max gain = 15 pts)
        // Tier 2 (+25%) locks 40% of gains: 50 + (15 * 0.40) = 56.0
        // Tier 5 (Chandelier, +20%) sets floor: 65 - (15 * 0.20) = 62.0
        // SL ratchets to 62.0!
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26NOV25000CE", 65.0m);

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var pos = await db.PaperPositions.FindAsync(posId);
            pos!.StopLossPrice.Should().Be(62.0m);
        }

        // 2. Price retraces from 65 to 61 (crosses below 62.0 SL)
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26NOV25000CE", 61.0m);

        // Assert: Auto-closed with TrailingStopHit and positive profit on small capital!
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var pos = await db.PaperPositions.FindAsync(posId);
            pos!.IsOpen.Should().BeFalse();

            var trade = await db.PaperTrades.FirstOrDefaultAsync(t => t.InstrumentKey == "NSE_FO|NIFTY26NOV25000CE");
            trade.Should().NotBeNull();
            trade!.ExitReason.Should().Be("TrailingStopHit");
            trade.GrossPnL.Should().BeGreaterThan(600m); // ~11-12 pts * 65 = ~700+
        }
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_CalculatesWinRateProfitFactorAndMetrics()
    {
        // Arrange: Insert 2 winning trades and 1 losing trade
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperTrades.AddRange(
                new PaperTrade { GrossPnL = 2000m, TotalCharges = 60m, NetPnL = 1940m, ExitTimeUtc = DateTime.UtcNow.AddHours(-2) },
                new PaperTrade { GrossPnL = 1500m, TotalCharges = 60m, NetPnL = 1440m, ExitTimeUtc = DateTime.UtcNow.AddHours(-1) },
                new PaperTrade { GrossPnL = -1000m, TotalCharges = 60m, NetPnL = -1060m, ExitTimeUtc = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();
        }

        // Act
        var summary = await _paperEngine.GetPortfolioSummaryAsync();

        // Assert
        summary.TotalTrades.Should().Be(3);
        summary.WinningTrades.Should().Be(2);
        summary.LosingTrades.Should().Be(1);
        summary.WinRatePercent.Should().Be(66.67m);
        summary.TotalGrossPnL.Should().Be(2500m);
        summary.TotalChargesPaid.Should().Be(180m);
        summary.TotalNetPnL.Should().Be(2320m);
        summary.ProfitFactor.Should().BeGreaterThan(3.0m);
        summary.Expectancy.Should().Be(Math.Round(2320m / 3m, 2));
    }

    [Fact]
    public async Task ExecuteSignalAsync_PopulatesCompleteOrderExecutionResultWithCanonicalSymbol()
    {
        var signal = new StrategySignal
        {
            Id = Guid.NewGuid(),
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|64473",
            SelectedStrike = 25500m,
            SelectedOptionType = OptionType.CE,
            SelectedExpiry = new DateTime(2026, 11, 23),
            EntryPremium = 60.0m,
            StopLossPremium = 48.0m,
            Target1Premium = 120.0m,
            RecommendedQuantity = 65,
            Confidence = 80m
        };

        _riskServiceMock.Setup(r => r.ValidatePreTradeAsync(It.IsAny<StrategySignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreTradeRiskValidationResult { IsApproved = true, AllocatedQuantity = 65 });

        var result = await _paperEngine.ExecuteSignalAsync(signal, simulateSlippage: false);

        result.IsSuccess.Should().BeTrue();
        result.TradingSymbol.Should().Be("NIFTY 23 NOV 25500 CE");
        result.OptionType.Should().Be(OptionType.CE);
        result.StrikePrice.Should().Be(25500m);
        result.StopLossPrice.Should().Be(48.0m);
        result.TargetPrice.Should().Be(120.0m);
        result.Quantity.Should().Be(65);
    }

    [Fact]
    public async Task UpdatePositionPricesAsync_EarlyGain_TrimsRiskViaTier0()
    {
        // Arrange: Position opened at 200, initial SL at 175 (12.5% risk)
        var posId = Guid.NewGuid();
        var position = new PaperPosition
        {
            Id = posId,
            InstrumentKey = "NSE_FO|NIFTY26DEC26000CE",
            TradingSymbol = "NIFTY 01 DEC 26000 CE",
            OptionType = OptionType.CE,
            Quantity = 65,
            AverageBuyPrice = 200.0m,
            CurrentLtp = 200.0m,
            StopLossPrice = 175.0m,
            TargetPrice = 300.0m,
            IsOpen = true
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            db.PaperPositions.Add(position);
            await db.SaveChangesAsync();
        }

        // Act: Price moves up to 216 (+8% gain, triggering Tier 0 early risk trim)
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26DEC26000CE", 216.0m);

        // Assert: Stop Loss immediately ratcheted up to -5% (190.0m), slashing remaining risk!
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var pos = await db.PaperPositions.FindAsync(posId);
            pos!.StopLossPrice.Should().Be(190.0m);
        }
    }
}
