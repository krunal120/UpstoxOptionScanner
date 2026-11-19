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

        // Assert: Stop Loss trailed to Breakeven + 1.0 = 121.0
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var pos = await db.PaperPositions.FindAsync(posId);
            pos!.IsOpen.Should().BeTrue();
            pos.StopLossPrice.Should().Be(121.0m);
            pos.UnrealizedPnL.Should().Be((152m - 120m) * 65);
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
}
