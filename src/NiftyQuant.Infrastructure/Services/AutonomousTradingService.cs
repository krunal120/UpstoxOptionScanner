using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Common.Models.PaperTrading;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;

namespace NiftyQuant.Infrastructure.Services;

public class AutonomousTradingService : BackgroundService, IAutonomousTradingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IPaperTradingEngine _paperEngine;
    private readonly IRiskManagementService _riskService;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly ICandleAggregationEngine _candleEngine;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly IOptionsIntelligenceService _optionsIntelligence;
    private readonly IUpstoxClient _upstoxClient;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IMegaBullService _megaBullService;
    private readonly RiskOptions _riskOptions;
    private readonly ILogger<AutonomousTradingService> _logger;
    private readonly TimeZoneInfo _istZone;

    private DateTime _lastSquareOffDate = DateTime.MinValue;
    private DateTime _lastStrategyEvaluationTime = DateTime.MinValue;
    private DateTime? _lastExecutionTime;
    private int _totalOrdersPunchedToday = 0;
    private int _totalOrdersExitedToday = 0;

    public bool IsEnabled { get; set; } = true;
    public string ExecutionMode { get; set; } = "Paper";
    public decimal MinConfidenceThreshold { get; set; } = 65.0m;
    public bool IsMarketHoursOnly { get; set; } = false;

    public AutonomousTradingService(
        IServiceScopeFactory scopeFactory,
        IStrategyEngine strategyEngine,
        IPaperTradingEngine paperEngine,
        IRiskManagementService riskService,
        IOptionChainSnapshotEngine snapshotEngine,
        ICandleAggregationEngine candleEngine,
        ITechnicalIndicatorService indicatorService,
        IOptionsIntelligenceService optionsIntelligence,
        IUpstoxClient upstoxClient,
        ISubscriptionManager subscriptionManager,
        IMegaBullService megaBullService,
        IOptions<RiskOptions> riskOptions,
        ILogger<AutonomousTradingService> logger)
    {
        _scopeFactory = scopeFactory;
        _strategyEngine = strategyEngine;
        _paperEngine = paperEngine;
        _riskService = riskService;
        _snapshotEngine = snapshotEngine;
        _candleEngine = candleEngine;
        _indicatorService = indicatorService;
        _optionsIntelligence = optionsIntelligence;
        _upstoxClient = upstoxClient;
        _subscriptionManager = subscriptionManager;
        _megaBullService = megaBullService;
        _riskOptions = riskOptions.Value;
        _logger = logger;
        _istZone = ResolveIstTimeZone();
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AutonomousTradingService starting... Mode={Mode}, AutoTrading={Enabled}, MinConfidence={Conf}%",
            ExecutionMode, IsEnabled, MinConfidenceThreshold);

        _strategyEngine.OnSignalGenerated += HandleStrategySignalAsync;
        _paperEngine.OnPositionClosed += HandlePositionClosedAsync;

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AutonomousTradingService stopping...");

        _strategyEngine.OnSignalGenerated -= HandleStrategySignalAsync;
        _paperEngine.OnPositionClosed -= HandlePositionClosedAsync;

        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutonomousTradingService execution loop active.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (IsEnabled)
                {
                    var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _istZone);

                    // 1. Check Intraday Auto Square-off at 15:15 IST
                    await CheckIntradaySquareOffAsync(istNow, stoppingToken);

                    // 2. Monitor Open Positions (SL, Target, Trailing SL)
                    await MonitorOpenPositionsAsync(stoppingToken);

                    // 3. Periodic Strategy Evaluation (every 15 seconds)
                    if (DateTime.UtcNow - _lastStrategyEvaluationTime >= TimeSpan.FromSeconds(15))
                    {
                        _lastStrategyEvaluationTime = DateTime.UtcNow;
                        await TriggerAutonomousEvaluationAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutonomousTradingService loop");
            }

            await Task.Delay(3000, stoppingToken);
        }
    }

    private async Task HandleStrategySignalAsync(StrategySignal signal)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Auto-trading is paused; ignoring incoming signal {SignalId}", signal.Id);
            return;
        }

        if (signal.IsExecuted) return;

        bool isBuyCe = signal.Direction == SignalDirection.BuyCe || signal.Direction == SignalDirection.StrongBuyCe;
        bool isBuyPe = signal.Direction == SignalDirection.BuyPe || signal.Direction == SignalDirection.StrongBuyPe;

        if (!isBuyCe && !isBuyPe) return;

        if (signal.Confidence < MinConfidenceThreshold)
        {
            _logger.LogInformation("[AUTO-TRADER] Signal {Direction} confidence {Conf}% below threshold {Min}%. Skipping.",
                signal.Direction, signal.Confidence, MinConfidenceThreshold);
            return;
        }

        if (IsMarketHoursOnly && !IsTradingHours())
        {
            _logger.LogInformation("[AUTO-TRADER] Outside market hours (09:20 - 15:10 IST). Skipping execution.");
            return;
        }

        // Expiry Validation: Strictly trade CURRENT active weekly expiry only (no previous/expired, no next/distant expiries)
        var todayUtc = DateTime.UtcNow.Date;
        if (signal.SelectedExpiry.Date < todayUtc)
        {
            _logger.LogWarning("[AUTO-TRADER EXPIRY VETO] Rejected order for {Symbol}: Expiry {Exp:dd-MMM-yyyy} is in the past.",
                signal.SelectedInstrumentKey, signal.SelectedExpiry);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_subscriptionManager.CurrentExpiry) &&
            DateTime.TryParse(_subscriptionManager.CurrentExpiry, out var activeExp) &&
            signal.SelectedExpiry.Date != activeExp.Date)
        {
            _logger.LogWarning("[AUTO-TRADER EXPIRY VETO] Rejected order for {Symbol}: Expiry {Exp:dd-MMM-yyyy} does not match current active weekly expiry {ActiveExp:dd-MMM-yyyy}. Non-current expiries are strictly disabled.",
                signal.SelectedInstrumentKey, signal.SelectedExpiry, activeExp);
            return;
        }

        try
        {
            // Check open positions to manage concurrent trades and reverse signals
            var openPositions = await _paperEngine.GetOpenPositionsAsync();

            // Reverse position handling: If we hold CE and get a strong PE signal (or vice versa), close opposite position
            foreach (var pos in openPositions)
            {
                bool isOpposite = (isBuyCe && pos.OptionType == OptionType.PE) ||
                                  (isBuyPe && pos.OptionType == OptionType.CE);
                if (isOpposite)
                {
                    _logger.LogInformation("[AUTO-TRADER] Signal reversal detected. Closing opposite position {Symbol} before entering {Direction}...",
                        pos.TradingSymbol, signal.Direction);
                    await _paperEngine.ClosePositionAsync(pos.Id, "SignalReversalExit", pos.CurrentLtp);
                }
            }

            // Check if already holding the exact contract
            if (openPositions.Any(p => p.InstrumentKey == signal.SelectedInstrumentKey && p.IsOpen))
            {
                _logger.LogInformation("[AUTO-TRADER] Already holding active position for {Key}. Avoiding duplicate entry.",
                    signal.SelectedInstrumentKey);
                return;
            }

            // Pre-trade risk validation
            var riskValidation = await _riskService.ValidatePreTradeAsync(signal);
            if (!riskValidation.IsApproved)
            {
                _logger.LogWarning("[AUTO-TRADER RISK VETO] Order rejected: {Reasons}",
                    string.Join("; ", riskValidation.VetoReasons));
                return;
            }

            // Punch the order automatically
            _logger.LogInformation("[AUTO-TRADER PUNCHING ORDER] Initiating BUY for {Symbol} | Direction: {Dir} | Confidence: {Conf}% | Entry: ₹{Entry} | SL: ₹{SL} | Target: ₹{Tgt}",
                signal.SelectedInstrumentKey, signal.Direction, signal.Confidence, signal.EntryPremium, signal.StopLossPremium, signal.Target1Premium);

            var execResult = await _paperEngine.ExecuteSignalAsync(signal, simulateSlippage: true);
            if (execResult.IsSuccess)
            {
                _totalOrdersPunchedToday++;
                _lastExecutionTime = DateTime.UtcNow;
                _logger.LogInformation("[AUTO-TRADER ORDER FILLED] Successfully punched BUY {Symbol} (Qty: {Qty}) @ ₹{Price:F2} | Order ID: {OrderId} | SL: ₹{SL:F2} | Tgt: ₹{Tgt:F2}",
                    execResult.TradingSymbol, execResult.Quantity, execResult.ExecutedPrice, execResult.OrderId, execResult.StopLossPrice, execResult.TargetPrice);

                // Mirror order to MegaBull external paper trading platform
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _megaBullService.PlaceOrderAsync(
                            execResult.TradingSymbol,
                            signal.SelectedOptionType.ToString(),
                            signal.SelectedStrike,
                            signal.SelectedExpiry,
                            execResult.Quantity,
                            "BUY");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[MEGABULL] Failed to mirror BUY order to MegaBull");
                    }
                });
            }
            else
            {
                _logger.LogWarning("[AUTO-TRADER ORDER FAILED] Could not fill order: {Message}", execResult.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-punching order for signal {SignalId}", signal.Id);
        }
    }

    private async Task MonitorOpenPositionsAsync(CancellationToken ct)
    {
        var openPositions = await _paperEngine.GetOpenPositionsAsync(ct);
        if (openPositions.Count == 0) return;

        OptionChainSnapshot? snapshot = null;
        try
        {
            snapshot = await _snapshotEngine.GetLatestSnapshotAsync("NSE_INDEX|Nifty 50", ct);
        }
        catch
        {
            // Snapshot lookup optional
        }

        foreach (var pos in openPositions)
        {
            decimal ltp = 0m;

            // 1. Direct quote lookup
            try
            {
                var quote = await _upstoxClient.GetMarketQuoteAsync(pos.InstrumentKey, ct);
                if (quote != null && quote.LastPrice > 0m)
                {
                    ltp = quote.LastPrice;
                }
            }
            catch
            {
                // Fallback below
            }

            // 2. Snapshot quote fallback by strike and option type
            if (ltp <= 0m && snapshot?.Quotes != null && pos.StrikePrice > 0m)
            {
                var matched = snapshot.Quotes.FirstOrDefault(q =>
                    q.StrikePrice == pos.StrikePrice &&
                    q.OptionType == pos.OptionType);

                if (matched != null && matched.Ltp > 0m)
                {
                    ltp = matched.Ltp;
                }
            }

            if (ltp <= 0m)
            {
                ltp = pos.CurrentLtp;
            }

            if (ltp > 0m)
            {
                await _paperEngine.UpdatePositionPricesAsync(pos.InstrumentKey, ltp, ct);
            }
        }
    }

    private async Task TriggerAutonomousEvaluationAsync(CancellationToken ct)
    {
        const string underlyingKey = "NSE_INDEX|Nifty 50";
        try
        {
            var context = await BuildStrategyAnalysisContextAsync(underlyingKey, ct);
            if (context != null)
            {
                await _strategyEngine.EvaluateStrategiesAsync(context, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Autonomous evaluation tick check failed");
        }
    }

    private async Task<StrategyAnalysisContext?> BuildStrategyAnalysisContextAsync(string underlyingKey, CancellationToken ct)
    {
        var candles1m = await GetEffectiveCandlesAsync(underlyingKey, CandleTimeframe.OneMinute, 3, ct);
        var candles5m = await GetEffectiveCandlesAsync(underlyingKey, CandleTimeframe.FiveMinute, 3, ct);
        var candles15m = await GetEffectiveCandlesAsync(underlyingKey, CandleTimeframe.FifteenMinute, 5, ct);

        if (candles1m.Count == 0 && candles5m.Count == 0)
        {
            return null;
        }

        var currentExpiry = _subscriptionManager.CurrentExpiry;
        var snapshot = (!string.IsNullOrWhiteSpace(currentExpiry)
            ? await _snapshotEngine.GetLatestSnapshotAsync(underlyingKey, currentExpiry, ct)
            : null) ?? await _snapshotEngine.GetLatestSnapshotAsync(underlyingKey, ct);
        var optionsReport = snapshot != null
            ? _optionsIntelligence.AnalyzeSnapshot(snapshot)
            : new OptionsIntelligenceReportDto();

        DateTime todayUtc = DateTime.UtcNow.Date;
        var todayCandles = candles1m.Where(c => c.CandleTimeUtc.Date == todayUtc).ToList();

        decimal currentSpot = snapshot?.UnderlyingPrice > 0m
            ? snapshot.UnderlyingPrice
            : (candles1m.Count > 0 ? candles1m[^1].Close : 25500m);

        decimal intradayHigh = todayCandles.Count > 0 ? todayCandles.Max(c => c.High) : currentSpot;
        decimal intradayLow = todayCandles.Count > 0 ? todayCandles.Min(c => c.Low) : currentSpot;

        // Calculate Previous Day High, Low & Close for CPR and Breakout evaluation
        var prevDayCandles = candles15m.Where(c => c.CandleTimeUtc.Date < todayUtc).ToList();
        var lastPrevDate = prevDayCandles.Count > 0 ? prevDayCandles.Max(c => c.CandleTimeUtc.Date) : DateTime.MinValue;
        var lastDayCandles = prevDayCandles.Where(c => c.CandleTimeUtc.Date == lastPrevDate).ToList();
        decimal prevDayHigh = lastDayCandles.Count > 0 ? lastDayCandles.Max(c => c.High) : 0m;
        decimal prevDayLow = lastDayCandles.Count > 0 ? lastDayCandles.Min(c => c.Low) : 0m;
        decimal prevDayClose = lastDayCandles.Count > 0 ? lastDayCandles.Last().Close : 0m;

        CprResult? dailyCpr = null;
        if (lastDayCandles.Count > 0 && prevDayHigh > 0m && prevDayLow > 0m)
        {
            dailyCpr = _indicatorService.CalculateDailyCpr(prevDayHigh, prevDayLow, prevDayClose);
        }

        var mtfState = _indicatorService.CalculateMultiTimeframeState(
            underlyingKey, candles1m, candles5m, candles15m, dailyCpr);

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
            // Fallback
        }

        return new StrategyAnalysisContext
        {
            UnderlyingKey = underlyingKey,
            CurrentPrice = currentSpot,
            IndiaVix = indiaVix,
            MultiTimeframeState = mtfState,
            OptionsReport = optionsReport,
            IntradayHigh = intradayHigh,
            IntradayLow = intradayLow,
            PreviousDayHigh = prevDayHigh,
            PreviousDayLow = prevDayLow,
            PreviousDayClose = prevDayClose,
            EvaluationTimeUtc = DateTime.UtcNow
        };
    }

    private async Task<IReadOnlyList<MarketCandle>> GetEffectiveCandlesAsync(
        string instrumentKey,
        CandleTimeframe timeframe,
        int days,
        CancellationToken ct)
    {
        var liveCandles = _candleEngine.GetRecentCandles(instrumentKey, timeframe, 150);
        if (liveCandles.Count >= 75)
        {
            return liveCandles;
        }

        var (unit, interval) = timeframe switch
        {
            CandleTimeframe.OneMinute => ("minutes", 1),
            CandleTimeframe.FiveMinute => ("minutes", 5),
            CandleTimeframe.FifteenMinute => ("minutes", 15),
            _ => ("minutes", 1)
        };

        var now = DateTime.Now;
        var toDate = now.ToString("yyyy-MM-dd");
        var fromDate = now.AddDays(-days).ToString("yyyy-MM-dd");

        var allCandles = new Dictionary<DateTime, MarketCandle>();

        // 1. Completed historical candles from previous trading sessions
        try
        {
            var historical = await _upstoxClient.GetHistoricalCandlesAsync(
                instrumentKey, unit, interval, toDate, fromDate, ct);
            foreach (var c in historical)
            {
                allCandles[c.Timestamp] = new MarketCandle
                {
                    InstrumentKey = instrumentKey,
                    Timeframe = timeframe,
                    CandleTimeUtc = c.Timestamp,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close,
                    Volume = c.Volume,
                    OpenInterest = c.OpenInterest,
                    IsFinalized = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch historical candles for {Key}", instrumentKey);
        }

        // 2. Today's live intraday candles from Upstox Intraday Candle API
        try
        {
            var intraday = await _upstoxClient.GetIntradayCandlesAsync(
                instrumentKey, unit, interval, ct);
            foreach (var c in intraday)
            {
                allCandles[c.Timestamp] = new MarketCandle
                {
                    InstrumentKey = instrumentKey,
                    Timeframe = timeframe,
                    CandleTimeUtc = c.Timestamp,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close,
                    Volume = c.Volume,
                    OpenInterest = c.OpenInterest,
                    IsFinalized = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch intraday candles for {Key}", instrumentKey);
        }

        // 3. Overlay any live WebSocket candles
        foreach (var c in liveCandles)
        {
            allCandles[c.CandleTimeUtc] = c;
        }

        return allCandles.Values.OrderBy(c => c.CandleTimeUtc).ToList();
    }

    private Task HandlePositionClosedAsync(PaperTrade trade)
    {
        _totalOrdersExitedToday++;
        _logger.LogInformation("[AUTO-TRADER POSITION CLOSED] {Symbol} ({Qty} shares) exited @ ₹{Exit:F2} (Entry: ₹{Entry:F2}) | Gross: ₹{Gross:F2} | Charges: ₹{Charges:F2} | Net PnL: ₹{Net:F2} ({Return:F1}%) | Exit Reason: {Reason} | Duration: {Mins} mins",
            trade.TradingSymbol, trade.Quantity, trade.ExitPrice, trade.EntryPrice, trade.GrossPnL, trade.TotalCharges, trade.NetPnL, trade.ReturnPercent, trade.ExitReason, trade.DurationMinutes);

        // Mirror exit order to MegaBull
        _ = Task.Run(async () =>
        {
            try
            {
                await _megaBullService.ClosePositionAsync(
                    trade.TradingSymbol ?? string.Empty,
                    trade.OptionType.ToString(),
                    trade.StrikePrice,
                    trade.ExpiryDate,
                    trade.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MEGABULL] Failed to mirror exit order to MegaBull");
            }
        });

        return Task.CompletedTask;
    }

    private async Task CheckIntradaySquareOffAsync(DateTime istNow, CancellationToken ct)
    {
        var squareOffTime = new TimeSpan(15, 15, 0);
        var marketCloseTime = new TimeSpan(15, 30, 0);

        if (istNow.TimeOfDay >= squareOffTime && istNow.TimeOfDay < marketCloseTime)
        {
            if (_lastSquareOffDate.Date != istNow.Date)
            {
                _lastSquareOffDate = istNow.Date;
                _logger.LogInformation("[AUTONOMOUS INTRADAY AUTO SQUARE-OFF] 15:15 IST reached. Closing all open positions...");
                await AutoSquareOffAllAsync("IntradaySquareOff1515", ct);
            }
        }
    }

    public async Task<int> AutoSquareOffAllAsync(string reason = "AutonomousSquareOff", CancellationToken ct = default)
    {
        var results = await _paperEngine.SquareOffAllPositionsAsync(reason, ct);
        _logger.LogInformation("[AUTO-TRADER] Squared off {Count} positions ({Reason})", results.Count, reason);
        return results.Count;
    }

    public Task SetEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        IsEnabled = enabled;
        _logger.LogInformation("Autonomous auto-trading status changed: IsEnabled={Enabled}", enabled);
        return Task.CompletedTask;
    }

    public AutonomousTradingStatusDto GetStatus()
    {
        return new AutonomousTradingStatusDto
        {
            IsEnabled = IsEnabled,
            ExecutionMode = ExecutionMode,
            MinConfidenceThreshold = MinConfidenceThreshold,
            IsMarketHoursOnly = IsMarketHoursOnly,
            LastExecutionTime = _lastExecutionTime,
            TotalOrdersPunchedToday = _totalOrdersPunchedToday,
            TotalOrdersExitedToday = _totalOrdersExitedToday,
            ActivePositionsCount = _paperEngine.GetOpenPositionsAsync().GetAwaiter().GetResult().Count,
            StatusMessage = IsEnabled ? "Autonomous Trader Active (Simulated Paper Execution)" : "Autonomous Trader Paused"
        };
    }

    public async Task<OrderExecutionResult> ForceTriggerSignalAndExecuteAsync(ForceSignalExecutionRequest request, CancellationToken ct = default)
    {
        bool isCe = string.Equals(request.OptionType, "CE", StringComparison.OrdinalIgnoreCase);
        decimal strike = request.StrikePrice ?? 25500m;
        decimal premium = request.Premium ?? 125.50m;
        decimal sl = Math.Max(1.0m, Math.Round(premium - 25m, 2));
        decimal t1 = Math.Round(premium + 50m, 2);
        decimal t2 = Math.Round(premium + 80m, 2);
        string instrumentKey = $"NSE_FO|NIFTY{strike:0}{(isCe ? "CE" : "PE")}";
        string tradingSymbol = $"NIFTY {strike:0} {(isCe ? "CE" : "PE")}";

        DateTime selectedExpiry = !string.IsNullOrWhiteSpace(_subscriptionManager.CurrentExpiry) && DateTime.TryParse(_subscriptionManager.CurrentExpiry, out var parsedExp)
            ? parsedExp.Date
            : new DateTime(2026, 11, 23);

        var signal = new StrategySignal
        {
            Id = Guid.NewGuid(),
            SignalTimeUtc = DateTime.UtcNow,
            UnderlyingKey = "NSE_INDEX|Nifty 50",
            UnderlyingPrice = 25500m,
            Direction = isCe ? SignalDirection.BuyCe : SignalDirection.BuyPe,
            StrategyFamily = StrategyFamily.TrendFollowing,
            MarketRegime = MarketRegimeType.StrongBullish,
            BullishScore = isCe ? request.Confidence : 20m,
            BearishScore = !isCe ? request.Confidence : 20m,
            Confidence = request.Confidence,
            SelectedInstrumentKey = instrumentKey,
            SelectedStrike = strike,
            SelectedOptionType = isCe ? OptionType.CE : OptionType.PE,
            SelectedExpiry = selectedExpiry,
            EntryPremium = premium,
            StopLossPremium = sl,
            Target1Premium = t1,
            Target2Premium = t2,
            RecommendedQuantity = 75,
            LotSize = 75,
            RiskAmount = (premium - sl) * 75,
            RewardToRiskRatio = 2.0m,
            ConfigurationVersion = "1.0",
            IsExecuted = false,
            Reasons = new List<SignalReason>
            {
                new() { Category = "Manual/Test", ScoreContribution = request.Confidence, Description = "Manual simulated trigger for end-to-end testing." }
            }
        };

        _logger.LogInformation("[AUTO-TRADER FORCE EXECUTE] Manually triggered {Symbol} order punch...", tradingSymbol);
        var result = await _paperEngine.ExecuteSignalAsync(signal, simulateSlippage: true, ct);
        if (result.IsSuccess)
        {
            _totalOrdersPunchedToday++;
            _lastExecutionTime = DateTime.UtcNow;

            try
            {
                var megaBullOrderId = await _megaBullService.PlaceOrderAsync(
                    result.TradingSymbol,
                    isCe ? "CE" : "PE",
                    strike,
                    signal.SelectedExpiry,
                    result.Quantity,
                    "BUY",
                    ct);

                if (!string.IsNullOrEmpty(megaBullOrderId))
                {
                    result.Message += $" | MegaBull Order #{megaBullOrderId} Placed Successfully";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MEGABULL] Failed to mirror test BUY order to MegaBull");
            }
        }

        return result;
    }

    private bool IsTradingHours()
    {
        var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _istZone);
        if (istNow.DayOfWeek == DayOfWeek.Saturday || istNow.DayOfWeek == DayOfWeek.Sunday) return false;
        var start = new TimeSpan(9, 20, 0);
        var end = new TimeSpan(15, 10, 0);
        return istNow.TimeOfDay >= start && istNow.TimeOfDay <= end;
    }

    private static TimeZoneInfo ResolveIstTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
            catch { return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "India Standard Time"); }
        }
    }
}
