using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Worker;

public class Worker : BackgroundService
{
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ILogger<Worker> _logger;
    private long _tickCount = 0;

    public Worker(
        IUpstoxWebSocketClient webSocketClient,
        ISubscriptionManager subscriptionManager,
        ILogger<Worker> logger)
    {
        _webSocketClient = webSocketClient;
        _subscriptionManager = subscriptionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Market Data Feed Worker starting...");

        _webSocketClient.OnTickReceived += HandleTickAsync;
        _webSocketClient.OnConnectionStateChanged += HandleConnectionStateAsync;

        try
        {
            // Connect WebSocket
            await _webSocketClient.ConnectAsync(stoppingToken);

            // Initialize Dynamic ATM Subscription
            await _subscriptionManager.InitializeAsync(stoppingToken);

            _logger.LogInformation("Worker subscribed to {Count} instruments around ATM strike {Atm}",
                _subscriptionManager.TrackedKeys.Count,
                _subscriptionManager.CurrentAtmStrike);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);

                if (_logger.IsEnabled(LogLevel.Information) && _tickCount > 0)
                {
                    _logger.LogInformation("Market Feed Active. Total Ticks Processed: {Count}. ATM: {Atm}",
                        _tickCount,
                        _subscriptionManager.CurrentAtmStrike);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Market Data Feed Worker stopping due to cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Market Data Feed Worker");
        }
        finally
        {
            await _webSocketClient.DisconnectAsync(CancellationToken.None);
        }
    }

    private async Task HandleTickAsync(MarketTick tick)
    {
        Interlocked.Increment(ref _tickCount);

        if (tick.InstrumentKey == "NSE_INDEX|Nifty 50" && tick.Ltp > 0m)
        {
            // Dynamically evaluate ATM strike shift
            await _subscriptionManager.OnSpotPriceUpdateAsync(tick.Ltp);
        }
    }

    private Task HandleConnectionStateAsync(bool connected, string reason)
    {
        _logger.LogInformation("Upstox WebSocket Connection State: {State}. Reason: {Reason}",
            connected ? "Connected" : "Disconnected", reason);
        return Task.CompletedTask;
    }
}
