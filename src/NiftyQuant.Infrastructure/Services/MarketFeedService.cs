using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Infrastructure.Services;

/// <summary>
/// Hosted background service that manages the Upstox Market Data Feed V3 WebSocket connection,
/// dynamically subscribes to NIFTY 50 spot and ATM option strikes, and pipes real-time ticks
/// into the Candle Aggregation Engine, Subscription Manager, and Paper Trading Engine.
/// </summary>
public class MarketFeedService : BackgroundService
{
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ICandleAggregationEngine _candleEngine;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly IPaperTradingEngine _paperEngine;
    private readonly ILogger<MarketFeedService> _logger;

    private long _tickCount = 0;
    private DateTime _lastSnapshotTime = DateTime.MinValue;
    private bool _hasWarnedAboutToken = false;

    public MarketFeedService(
        IUpstoxWebSocketClient webSocketClient,
        ISubscriptionManager subscriptionManager,
        ICandleAggregationEngine candleEngine,
        IOptionChainSnapshotEngine snapshotEngine,
        IPaperTradingEngine paperEngine,
        ILogger<MarketFeedService> logger)
    {
        _webSocketClient = webSocketClient;
        _subscriptionManager = subscriptionManager;
        _candleEngine = candleEngine;
        _snapshotEngine = snapshotEngine;
        _paperEngine = paperEngine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketFeedService (Upstox WebSocket Manager) initializing...");

        _webSocketClient.OnTickReceived += HandleTickAsync;
        _webSocketClient.OnConnectionStateChanged += HandleConnectionStateAsync;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_webSocketClient.IsConnected)
                {
                    try
                    {
                        _logger.LogInformation("[Upstox WebSocket] Attempting connection to Upstox Market Data Feed V3...");
                        await _webSocketClient.ConnectAsync(stoppingToken);

                        _logger.LogInformation("[Upstox WebSocket] Initializing dynamic ATM option strike subscriptions...");
                        await _subscriptionManager.InitializeAsync(stoppingToken);

                        _logger.LogInformation("[Upstox WebSocket] Connected and subscribed to {Count} instruments around ATM {Atm}",
                            _subscriptionManager.TrackedKeys.Count,
                            _subscriptionManager.CurrentAtmStrike);

                        // Capture initial snapshot
                        await _snapshotEngine.CaptureAndPersistSnapshotAsync(expiryDate: _subscriptionManager.CurrentExpiry, ct: stoppingToken);
                        _lastSnapshotTime = DateTime.UtcNow;
                        _hasWarnedAboutToken = false;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!_hasWarnedAboutToken)
                        {
                            _logger.LogWarning("[Upstox WebSocket] Live feed currently not active ({Error}). Note: Ensure Upstox:AccessToken is configured for live streaming. Simulated paper trading and strategy testing remain fully operational. Retrying in 30s...",
                                ex.Message);
                            _hasWarnedAboutToken = true;
                        }
                        else
                        {
                            _logger.LogDebug("[Upstox WebSocket] Reconnect attempt failed: {Error}. Retrying in 30s...", ex.Message);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                        continue;
                    }
                }

                // Periodic Option Chain snapshot capture every 60 seconds while connected
                if (_webSocketClient.IsConnected && (DateTime.UtcNow - _lastSnapshotTime).TotalSeconds >= 60)
                {
                    _lastSnapshotTime = DateTime.UtcNow;
                    foreach (var profile in IndexRegistry.SupportedIndices.Where(p => p.IsEnabled))
                    {
                        try
                        {
                            var expiry = _subscriptionManager.GetCurrentExpiry(profile.UnderlyingKey);
                            await _snapshotEngine.CaptureAndPersistSnapshotAsync(underlyingKey: profile.UnderlyingKey, expiryDate: expiry, ct: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogTrace(ex, "Failed periodic option chain snapshot capture for {Key}", profile.UnderlyingKey);
                        }
                    }
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MarketFeedService shutting down gracefully.");
        }
        finally
        {
            _webSocketClient.OnTickReceived -= HandleTickAsync;
            _webSocketClient.OnConnectionStateChanged -= HandleConnectionStateAsync;

            if (_webSocketClient.IsConnected)
            {
                await _webSocketClient.DisconnectAsync(CancellationToken.None);
            }
        }
    }

    private async Task HandleTickAsync(MarketTick tick)
    {
        Interlocked.Increment(ref _tickCount);

        // 1. Process tick into candle aggregation engine (1m, 5m, 15m)
        await _candleEngine.ProcessTickAsync(tick);

        // 2. Dynamic ATM Strike adjustment when spot moves
        if (tick.Ltp > 0m && IndexRegistry.IsSupportedIndex(tick.InstrumentKey))
        {
            await _subscriptionManager.OnSpotPriceUpdateAsync(tick.InstrumentKey, tick.Ltp);
        }

        // 3. Update active paper positions in real-time for trailing stop / target triggers
        try
        {
            await _paperEngine.UpdatePositionPricesAsync(tick.InstrumentKey, tick.Ltp);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error updating paper position price for {Key}", tick.InstrumentKey);
        }

        if (_tickCount > 0 && (_tickCount % 500 == 0))
        {
            _logger.LogInformation("[Upstox WebSocket] Stream Healthy | Ticks Processed: {Count} | Current ATM: {Atm}",
                _tickCount, _subscriptionManager.CurrentAtmStrike);
        }
    }

    private Task HandleConnectionStateAsync(bool connected, string reason)
    {
        _logger.LogInformation("[Upstox WebSocket] Connection State Changed: {State} (Reason: {Reason})",
            connected ? "Connected" : "Disconnected", reason);
        return Task.CompletedTask;
    }
}
