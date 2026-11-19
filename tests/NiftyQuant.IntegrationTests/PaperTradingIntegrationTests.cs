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

public class PaperTradingIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPaperTradingEngine _paperEngine;
    private readonly IRiskManagementService _riskService;

    public PaperTradingIntegrationTests()
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
        _paperEngine = _serviceProvider.GetRequiredService<IPaperTradingEngine>();
        _riskService = _serviceProvider.GetRequiredService<IRiskManagementService>();
    }

    [Fact]
    public async Task PaperTradingLifecycle_ExecuteUpdateAndClose_SucceedsEndToEndInSqlServer()
    {
        // 1. Reset daily limits first to ensure circuit breaker is clean
        await _riskService.ResetDailyLimitsAsync();

        // Ensure any pre-existing open positions are squared off so max concurrent positions passes
        await _paperEngine.SquareOffAllPositionsAsync("TestSetupReset");

        // 2. Prepare a compliant StrategySignal (in-session time)
        var inSessionUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 5, 30, 0, DateTimeKind.Utc);
        var signal = new StrategySignal
        {
            Id = Guid.NewGuid(),
            Direction = SignalDirection.BuyCe,
            SelectedInstrumentKey = "NSE_FO|NIFTY26NOV24200CE",
            SelectedOptionType = OptionType.CE,
            SelectedStrike = 24200m,
            SelectedExpiry = new DateTime(2026, 11, 26),
            EntryPremium = 110.0m,
            StopLossPremium = 95.0m, // Risk = 15 pts
            Target1Premium = 140.0m,  // Reward = 30 pts -> 1:2.0
            SignalTimeUtc = inSessionUtc
        };

        // 3. Execute Paper Order
        var execResult = await _paperEngine.ExecuteSignalAsync(signal, simulateSlippage: true);
        execResult.Should().NotBeNull();
        execResult.IsSuccess.Should().BeTrue();
        execResult.PositionId.Should().NotBeNull();
        execResult.ExecutedPrice.Should().BeGreaterThan(110.0m, "slippage should be applied");

        Guid positionId = execResult.PositionId!.Value;

        // 4. Verify Open Position
        var openPositions = await _paperEngine.GetOpenPositionsAsync();
        openPositions.Should().Contain(p => p.Id == positionId && p.IsOpen);

        // 5. Update Position Prices
        await _paperEngine.UpdatePositionPricesAsync("NSE_FO|NIFTY26NOV24200CE", 125.0m);
        var updatedPositions = await _paperEngine.GetOpenPositionsAsync();
        var currentPos = updatedPositions.FirstOrDefault(p => p.Id == positionId);
        currentPos.Should().NotBeNull();
        currentPos!.CurrentLtp.Should().Be(125.0m);
        currentPos.UnrealizedPnL.Should().BeGreaterThan(0m);

        // 6. Close Position Manually
        var closeResult = await _paperEngine.ClosePositionAsync(positionId, "TestManualClose", 130.0m);
        closeResult.Should().NotBeNull();
        closeResult.IsSuccess.Should().BeTrue();
        closeResult.GrossPnL.Should().BeGreaterThan(0m);
        closeResult.NetPnL.Should().BeGreaterThan(0m);

        // 7. Verify Position is now Closed
        var openAfter = await _paperEngine.GetOpenPositionsAsync();
        openAfter.Should().NotContain(p => p.Id == positionId);

        // 8. Verify Portfolio Summary
        var portfolio = await _paperEngine.GetPortfolioSummaryAsync();
        portfolio.Should().NotBeNull();
        portfolio.TotalTrades.Should().BeGreaterThan(0);
    }
}
