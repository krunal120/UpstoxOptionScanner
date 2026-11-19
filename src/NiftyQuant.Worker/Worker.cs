using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Worker;

public class Worker : BackgroundService
{
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ICandleAggregationEngine _candleEngine;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly IOptionsIntelligenceService _optionsIntelligence;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IMarketRegimeClassifier _regimeClassifier;
    private readonly IRiskManagementService _riskService;
    private readonly IPaperTradingEngine _paperTradingEngine;
    private readonly IUpstoxClient _upstoxClient;
    private readonly ILogger<Worker> _logger;

    private readonly TimeZoneInfo _istZone;
    private long _tickCount = 0;
    private DateTime _lastSnapshotTime = DateTime.MinValue;
    private DateTime _lastStrategyEvaluationTime = DateTime.MinValue;
    private DateTime _lastSquareOffDate = DateTime.MinValue;

    public Worker(
        IUpstoxWebSocketClient webSocketClient,
        ISubscriptionManager subscriptionManager,
        ICandleAggregationEngine candleEngine,
        IOptionChainSnapshotEngine snapshotEngine,
        ITechnicalIndicatorService indicatorService,
        IOptionsIntelligenceService optionsIntelligence,
        IStrategyEngine strategyEngine,
        IMarketRegimeClassifier regimeClassifier,
        IRiskManagementService riskService,
        IPaperTradingEngine paperTradingEngine,
        IUpstoxClient upstoxClient,
        ILogger<Worker> logger)
    {
        _webSocketClient = webSocketClient;
        _subscriptionManager = subscriptionManager;
        _candleEngine = candleEngine;
        _snapshotEngine = snapshotEngine;
        _indicatorService = indicatorService;
        _optionsIntelligence = optionsIntelligence;
        _strategyEngine = strategyEngine;
        _regimeClassifier = regimeClassifier;
        _riskService = riskService;
        _paperTradingEngine = paperTradingEngine;
        _upstoxClient = upstoxClient;
        _logger = logger;

        _istZone = ResolveIstTimeZone();
    }

    private static TimeZoneInfo ResolveIstTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "India Standard Time");
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NiftyQuant Autonomous Trading Worker initializing...");

        _webSocketClient.OnTickReceived += HandleTickAsync;
        _webSocketClient.OnConnectionStateChanged += HandleConnectionStateAsync;
        _candleEngine.OnCandleFinalized += HandleCandleFinalizedAsync;

        try
        {
            // Connect Upstox WebSocket Streamer
            await _webSocketClient.ConnectAsync(stoppingToken);

            // Initialize Dynamic ATM Strike Subscription
            await _subscriptionManager.InitializeAsync(stoppingToken);

            _logger.LogInformation("Worker subscribed to {Count} instruments around ATM strike {Atm}",
                _subscriptionManager.TrackedKeys.Count,
                _subscriptionManager.CurrentAtmStrike);

            // Capture initial snapshot
            await _snapshotEngine.CaptureAndPersistSnapshotAsync(expiryDate: _subscriptionManager.CurrentExpiry, ct: stoppingToken);
            _lastSnapshotTime = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);

                var nowUtc = DateTime.UtcNow;
                var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), _istZone);

                // Check Intraday Mandatory Square-Off (15:15 IST)
                await CheckIntradaySquareOffAsync(nowIst, stoppingToken);

                // Check periodic Option Chain snapshot (every 60 seconds)
                if ((nowUtc - _lastSnapshotTime).TotalSeconds >= 60)
                {
                    _lastSnapshotTime = nowUtc;
                    try
                    {
                        await _snapshotEngine.CaptureAndPersistSnapshotAsync(expiryDate: _subscriptionManager.CurrentExpiry, ct: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed periodic option chain snapshot capture");
                    }
                }

                // Check periodic Strategy Evaluation during trading hours (every 60 seconds)
                if (IsTradingHours(nowIst) && (nowUtc - _lastStrategyEvaluationTime).TotalSeconds >= 60)
                {
                    _lastStrategyEvaluationTime = nowUtc;
                    await EvaluateAndExecuteStrategiesAsync(stoppingToken);
                }

                if (_logger.IsEnabled(LogLevel.Information) && _tickCount > 0 && (_tickCount % 500 == 0))
                {
                    _logger.LogInformation("NiftyQuant Autonomous Engine Running | Ticks Processed: {Count} | Current ATM: {Atm} | IST Time: {Ist:HH:mm:ss}",
                        _tickCount,
                        _subscriptionManager.CurrentAtmStrike,
                        nowIst);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("NiftyQuant Worker stopping gracefully due to cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal unexpected error in NiftyQuant Worker");
        }
        finally
        {
            _webSocketClient.OnTickReceived -= HandleTickAsync;
            _webSocketClient.OnConnectionStateChanged -= HandleConnectionStateAsync;
            _candleEngine.OnCandleFinalized -= HandleCandleFinalizedAsync;

            await _webSocketClient.DisconnectAsync(CancellationToken.None);
            _logger.LogInformation("NiftyQuant Autonomous Worker stopped.");
        }
    }

    private async Task HandleTickAsync(MarketTick tick)
    {
        Interlocked.Increment(ref _tickCount);

        // 1. Process tick into candle aggregation
        await _candleEngine.ProcessTickAsync(tick);

        // 2. Dynamic ATM Strike Shift
        if (tick.InstrumentKey == "NSE_INDEX|Nifty 50" && tick.Ltp > 0m)
        {
            await _subscriptionManager.OnSpotPriceUpdateAsync(tick.Ltp);
        }

        // 3. Update active paper positions for real-time trailing stop / target triggers
        try
        {
            await _paperTradingEngine.UpdatePositionPricesAsync(tick.InstrumentKey, tick.Ltp);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error updating paper position price for {Key}", tick.InstrumentKey);
        }
    }

    private async Task HandleCandleFinalizedAsync(MarketCandle candle)
    {
        _logger.LogInformation("[Candle Finalized] {Instrument} {Timeframe} | O: {Open} H: {High} L: {Low} C: {Close} Vol: {Volume} Time: {Time:HH:mm:ss} UTC",
            candle.InstrumentKey, candle.Timeframe, candle.Open, candle.High, candle.Low, candle.Close, candle.Volume, candle.CandleTimeUtc);

        // On 1m or 5m candle close for NIFTY 50 spot, trigger strategy evaluation
        if (candle.InstrumentKey == "NSE_INDEX|Nifty 50" &&
            (candle.Timeframe == CandleTimeframe.OneMinute || candle.Timeframe == CandleTimeframe.FiveMinute))
        {
            var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc), _istZone);
            if (IsTradingHours(nowIst))
            {
                await EvaluateAndExecuteStrategiesAsync(CancellationToken.None);
            }
        }
    }

    private async Task EvaluateAndExecuteStrategiesAsync(CancellationToken ct)
    {
        const string underlyingKey = "NSE_INDEX|Nifty 50";

        try
        {
            var context = await BuildStrategyContextAsync(underlyingKey, ct);
            if (context == null) return;

            var signal = await _strategyEngine.EvaluateStrategiesAsync(context, ct);
            if (signal == null) return;

            // Check if signal indicates an executable trade (Option Buying CE/PE only)
            bool isExecutable = signal.Direction == SignalDirection.BuyCe ||
                               signal.Direction == SignalDirection.StrongBuyCe ||
                               signal.Direction == SignalDirection.BuyPe ||
                               signal.Direction == SignalDirection.StrongBuyPe;

            if (!isExecutable)
            {
                _logger.LogDebug("Signal evaluation: {Direction} (Confidence: {Conf}%). No execution required.",
                    signal.Direction, signal.Confidence);
                return;
            }

            _logger.LogInformation("[SIGNAL DETECTED] {Direction} | Strategy: {Strategy} | Conf: {Conf}% | Instrument: {Key} | Entry: ₹{Entry} SL: ₹{SL} Tgt: ₹{Tgt} R:R: {RR}",
                signal.Direction,
                signal.StrategyFamily,
                signal.Confidence,
                signal.SelectedInstrumentKey,
                signal.EntryPremium,
                signal.StopLossPremium,
                signal.Target1Premium,
                signal.RewardToRiskRatio);

            // Validate Pre-Trade Risk Rules
            var riskResult = await _riskService.ValidatePreTradeAsync(signal, ct);
            if (!riskResult.IsApproved)
            {
                _logger.LogWarning("[RISK VETO] Trade execution rejected by Risk Management Engine: {Reason}",
                    string.Join("; ", riskResult.VetoReasons));
                return;
            }

            // Execute simulated paper trade
            var execResult = await _paperTradingEngine.ExecuteSignalAsync(signal, simulateSlippage: true, ct);
            if (execResult.IsSuccess)
            {
                _logger.LogInformation("[TRADE EXECUTED] Paper order filled for {Key} ({Qty} shares) @ ₹{Price:F2} | Order ID: {OrderId}",
                    execResult.InstrumentKey,
                    execResult.Quantity,
                    execResult.ExecutedPrice,
                    execResult.OrderId);
            }
            else
            {
                _logger.LogWarning("[EXECUTION REJECTED] Paper order could not be filled: {Message}",
                    execResult.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in autonomous strategy evaluation loop");
        }
    }

    private async Task CheckIntradaySquareOffAsync(DateTime nowIst, CancellationToken ct)
    {
        var squareOffTime = new TimeSpan(15, 15, 0);
        var marketCloseTime = new TimeSpan(15, 30, 0);

        if (nowIst.TimeOfDay >= squareOffTime && nowIst.TimeOfDay < marketCloseTime)
        {
            if (_lastSquareOffDate.Date != nowIst.Date)
            {
                _lastSquareOffDate = nowIst.Date;
                _logger.LogInformation("[INTRADAY AUTO SQUARE-OFF] Triggering mandatory 15:15 IST liquidation of all open positions...");

                try
                {
                    var results = await _paperTradingEngine.SquareOffAllPositionsAsync("IntradaySquareOff1515", ct);
                    _logger.LogInformation("[INTRADAY AUTO SQUARE-OFF] Completed. Liquidated {Count} active positions.", results.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during 15:15 IST intraday auto square-off");
                }
            }
        }
    }

    private static bool IsTradingHours(DateTime istTime)
    {
        var startTime = new TimeSpan(9, 20, 0);
        var endTime = new TimeSpan(15, 10, 0);
        var dayOfWeek = istTime.DayOfWeek;

        if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            return false;

        return istTime.TimeOfDay >= startTime && istTime.TimeOfDay <= endTime;
    }

    private async Task<StrategyAnalysisContext?> BuildStrategyContextAsync(string underlyingKey, CancellationToken ct)
    {
        try
        {
            var candles1m = _candleEngine.GetRecentCandles(underlyingKey, CandleTimeframe.OneMinute, 100);
            var candles5m = _candleEngine.GetRecentCandles(underlyingKey, CandleTimeframe.FiveMinute, 100);
            var candles15m = _candleEngine.GetRecentCandles(underlyingKey, CandleTimeframe.FifteenMinute, 100);

            if (candles1m.Count == 0 && candles5m.Count == 0 && candles15m.Count == 0)
            {
                return null;
            }

            var (dailyCpr, prevHigh, prevLow, prevClose) = await GetDailyLevelsAsync(underlyingKey, ct);

            var mtfState = _indicatorService.CalculateMultiTimeframeState(
                underlyingKey, candles1m, candles5m, candles15m, dailyCpr);

            var snapshot = await _snapshotEngine.GetLatestSnapshotAsync(underlyingKey, ct)
                           ?? await _snapshotEngine.CaptureAndPersistSnapshotAsync(underlyingKey, ct: ct);

            var optionsReport = snapshot != null
                ? _optionsIntelligence.AnalyzeSnapshot(snapshot)
                : new OptionsIntelligenceReportDto();

            decimal? indiaVix = null;
            try
            {
                var vixQuote = await _upstoxClient.GetMarketQuoteAsync("NSE_INDEX|India VIX", ct);
                if (vixQuote != null && vixQuote.LastPrice > 0m)
                {
                    indiaVix = vixQuote.LastPrice;
                }
            }
            catch
            {
                // Fallback to null
            }

            decimal currentSpot = snapshot?.UnderlyingPrice > 0m
                ? snapshot.UnderlyingPrice
                : (candles1m.Count > 0 ? candles1m[^1].Close : 0m);

            DateTime todayUtc = DateTime.UtcNow.Date;
            var todayCandles = candles1m.Where(c => c.CandleTimeUtc.Date == todayUtc).ToList();
            decimal intradayHigh = todayCandles.Count > 0 ? todayCandles.Max(c => c.High) : currentSpot;
            decimal intradayLow = todayCandles.Count > 0 ? todayCandles.Min(c => c.Low) : currentSpot;

            return new StrategyAnalysisContext
            {
                UnderlyingKey = underlyingKey,
                CurrentPrice = currentSpot,
                IndiaVix = indiaVix,
                MultiTimeframeState = mtfState,
                OptionsReport = optionsReport,
                PreviousDayHigh = prevHigh,
                PreviousDayLow = prevLow,
                PreviousDayClose = prevClose,
                IntradayHigh = intradayHigh,
                IntradayLow = intradayLow,
                EvaluationTimeUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build strategy analysis context in Worker");
            return null;
        }
    }

    private async Task<(CprResult? Cpr, decimal High, decimal Low, decimal Close)> GetDailyLevelsAsync(
        string instrumentKey,
        CancellationToken ct)
    {
        try
        {
            var now = DateTime.Now;
            var toDate = now.ToString("yyyy-MM-dd");
            var fromDate = now.AddDays(-7).ToString("yyyy-MM-dd");

            var dailyCandles = await _upstoxClient.GetHistoricalCandlesAsync(
                instrumentKey, "days", 1, toDate, fromDate, ct);

            if (dailyCandles.Count >= 2)
            {
                var prevDay = dailyCandles[^2];
                var cpr = _indicatorService.CalculateDailyCpr(prevDay.High, prevDay.Low, prevDay.Close);
                return (cpr, prevDay.High, prevDay.Low, prevDay.Close);
            }
            else if (dailyCandles.Count == 1)
            {
                var day = dailyCandles[0];
                var cpr = _indicatorService.CalculateDailyCpr(day.High, day.Low, day.Close);
                return (cpr, day.High, day.Low, day.Close);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute daily CPR levels in Worker");
        }

        return (null, 0m, 0m, 0m);
    }

    private Task HandleConnectionStateAsync(bool connected, string reason)
    {
        _logger.LogInformation("WebSocket State Changed: {State} (Reason: {Reason})",
            connected ? "Connected" : "Disconnected", reason);
        return Task.CompletedTask;
    }
}
