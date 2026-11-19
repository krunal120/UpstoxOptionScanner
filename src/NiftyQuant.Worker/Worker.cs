using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Worker;

public class Worker : BackgroundService
{
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ICandleAggregationEngine _candleEngine;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly ILogger<Worker> _logger;

    private long _tickCount = 0;
    private DateTime _lastSnapshotTime = DateTime.MinValue;

    public Worker(
        IUpstoxWebSocketClient webSocketClient,
        ISubscriptionManager subscriptionManager,
        ICandleAggregationEngine candleEngine,
        IOptionChainSnapshotEngine snapshotEngine,
        ILogger<Worker> logger)
    {
        _webSocketClient = webSocketClient;
        _subscriptionManager = subscriptionManager;
        _candleEngine = candleEngine;
        _snapshotEngine = snapshotEngine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Market Data Storage & Processing Worker starting...");

        _webSocketClient.OnTickReceived += HandleTickAsync;
        _webSocketClient.OnConnectionStateChanged += HandleConnectionStateAsync;
        _candleEngine.OnCandleFinalized += HandleCandleFinalizedAsync;

        try
        {
            // Connect WebSocket
            await _webSocketClient.ConnectAsync(stoppingToken);

            // Initialize Dynamic ATM Subscription
            await _subscriptionManager.InitializeAsync(stoppingToken);

            _logger.LogInformation("Worker subscribed to {Count} instruments around ATM strike {Atm}",
                _subscriptionManager.TrackedKeys.Count,
                _subscriptionManager.CurrentAtmStrike);

            // Capture initial option chain snapshot
            await _snapshotEngine.CaptureAndPersistSnapshotAsync(expiryDate: _subscriptionManager.CurrentExpiry, ct: stoppingToken);
            _lastSnapshotTime = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);

                // Periodic Option Chain snapshot every 1 minute
                if ((DateTime.UtcNow - _lastSnapshotTime).TotalSeconds >= 60)
                {
                    _lastSnapshotTime = DateTime.UtcNow;
                    _ = _snapshotEngine.CaptureAndPersistSnapshotAsync(expiryDate: _subscriptionManager.CurrentExpiry, ct: stoppingToken);
                }

                if (_logger.IsEnabled(LogLevel.Information) && _tickCount > 0)
                {
                    _logger.LogInformation("Live Processing Active. Ticks: {Count}. ATM: {Atm}",
                        _tickCount,
                        _subscriptionManager.CurrentAtmStrike);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker stopping due to cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Market Data Storage Worker");
        }
        finally
        {
            await _webSocketClient.DisconnectAsync(CancellationToken.None);
        }
    }

    private async Task HandleTickAsync(MarketTick tick)
    {
        Interlocked.Increment(ref _tickCount);

        // Process tick through the Candle Aggregation Engine
        await _candleEngine.ProcessTickAsync(tick);

        if (tick.InstrumentKey == "NSE_INDEX|Nifty 50" && tick.Ltp > 0m)
        {
            // Dynamically evaluate ATM strike shift
            await _subscriptionManager.OnSpotPriceUpdateAsync(tick.Ltp);
        }
    }

    private Task HandleCandleFinalizedAsync(MarketCandle candle)
    {
        _logger.LogInformation("[Candle Finalized] {Instrument} {Timeframe} | O: {Open} H: {High} L: {Low} C: {Close} Vol: {Volume} Time: {Time:HH:mm:ss} UTC",
            candle.InstrumentKey, candle.Timeframe, candle.Open, candle.High, candle.Low, candle.Close, candle.Volume, candle.CandleTimeUtc);
        return Task.CompletedTask;
    }

    private Task HandleConnectionStateAsync(bool connected, string reason)
    {
        _logger.LogInformation("WebSocket State: {State}. Reason: {Reason}",
            connected ? "Connected" : "Disconnected", reason);
        return Task.CompletedTask;
    }
}
