using Microsoft.AspNetCore.SignalR;
using NiftyQuant.Api.Hubs;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Api.Services;

public class SignalRBridgeService : IHostedService
{
    private readonly IHubContext<MarketHub> _hubContext;
    private readonly ICandleAggregationEngine _candleEngine;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IPaperTradingEngine _paperEngine;
    private readonly ILogger<SignalRBridgeService> _logger;

    public SignalRBridgeService(
        IHubContext<MarketHub> hubContext,
        ICandleAggregationEngine candleEngine,
        IOptionChainSnapshotEngine snapshotEngine,
        IStrategyEngine strategyEngine,
        IPaperTradingEngine paperEngine,
        ILogger<SignalRBridgeService> logger)
    {
        _hubContext = hubContext;
        _candleEngine = candleEngine;
        _snapshotEngine = snapshotEngine;
        _strategyEngine = strategyEngine;
        _paperEngine = paperEngine;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting SignalRBridgeService...");

        _candleEngine.OnCandleFinalized += HandleCandleFinalizedAsync;
        _snapshotEngine.OnSnapshotCaptured += HandleSnapshotCapturedAsync;
        _strategyEngine.OnSignalGenerated += HandleSignalGeneratedAsync;
        _paperEngine.OnOrderPlaced += HandleOrderPlacedAsync;
        _paperEngine.OnOrderExecuted += HandleOrderExecutedAsync;
        _paperEngine.OnPositionOpened += HandlePositionOpenedAsync;
        _paperEngine.OnPositionUpdated += HandlePositionUpdatedAsync;
        _paperEngine.OnPositionClosed += HandlePositionClosedAsync;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping SignalRBridgeService...");

        _candleEngine.OnCandleFinalized -= HandleCandleFinalizedAsync;
        _snapshotEngine.OnSnapshotCaptured -= HandleSnapshotCapturedAsync;
        _strategyEngine.OnSignalGenerated -= HandleSignalGeneratedAsync;
        _paperEngine.OnOrderPlaced -= HandleOrderPlacedAsync;
        _paperEngine.OnOrderExecuted -= HandleOrderExecutedAsync;
        _paperEngine.OnPositionOpened -= HandlePositionOpenedAsync;
        _paperEngine.OnPositionUpdated -= HandlePositionUpdatedAsync;
        _paperEngine.OnPositionClosed -= HandlePositionClosedAsync;

        return Task.CompletedTask;
    }

    private async Task HandleCandleFinalizedAsync(MarketCandle candle)
    {
        try
        {
            await _hubContext.Clients.Group("MarketFeed").SendAsync("ReceiveCandle", candle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting finalized candle over SignalR.");
        }
    }

    private async Task HandleSnapshotCapturedAsync(OptionChainSnapshot snapshot)
    {
        try
        {
            await _hubContext.Clients.Group("MarketFeed").SendAsync("ReceiveSnapshot", new
            {
                snapshot.Id,
                snapshot.UnderlyingKey,
                snapshot.UnderlyingPrice,
                snapshot.Pcr,
                snapshot.MaxPainStrike,
                snapshot.HighestCallOiStrike,
                snapshot.HighestPutOiStrike,
                snapshot.TotalCallOi,
                snapshot.TotalPutOi,
                snapshot.SnapshotTimeUtc,
                QuotesCount = snapshot.Quotes.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting snapshot over SignalR.");
        }
    }

    private async Task HandleSignalGeneratedAsync(StrategySignal signal)
    {
        try
        {
            await _hubContext.Clients.Group("MarketFeed").SendAsync("ReceiveSignal", signal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting strategy signal over SignalR.");
        }
    }

    private async Task HandleOrderPlacedAsync(PaperOrder order)
    {
        try
        {
            await _hubContext.Clients.Group("MarketFeed").SendAsync("ReceiveOrderPlaced", order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting order placed over SignalR.");
        }
    }

    private async Task HandleOrderExecutedAsync(PaperOrder order)
    {
        try
        {
            await _hubContext.Clients.Group("MarketFeed").SendAsync("ReceiveOrderExecuted", order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting order executed over SignalR.");
        }
    }

    private async Task HandlePositionOpenedAsync(PaperPosition position)
    {
        try
        {
            await _hubContext.Clients.Group("MarketFeed").SendAsync("ReceivePositionOpened", position);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting position opened over SignalR.");
        }
    }

    private async Task HandlePositionUpdatedAsync(PaperPosition position)
    {
        try
        {
            await _hubContext.Clients.Group("MarketFeed").SendAsync("ReceivePositionUpdated", position);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting position updated over SignalR.");
        }
    }

    private async Task HandlePositionClosedAsync(PaperTrade trade)
    {
        try
        {
            await _hubContext.Clients.Group("MarketFeed").SendAsync("ReceiveTradeClosed", trade);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting trade closed over SignalR.");
        }
    }
}
