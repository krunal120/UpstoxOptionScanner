using Microsoft.Extensions.Logging;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Backtesting;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Application.Services.Risk;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Infrastructure.Services;

public class BacktestingEngine : IBacktestingEngine
{
    private static readonly TimeZoneInfo IstZone = GetIstTimeZone();

    private readonly IUpstoxClient _upstoxClient;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly IMarketRegimeClassifier _regimeClassifier;
    private readonly IEnumerable<IStrategyEvaluator> _evaluators;
    private readonly PositionSizingCalculator _calculator;
    private readonly ILogger<BacktestingEngine> _logger;

    public BacktestingEngine(
        IUpstoxClient upstoxClient,
        ITechnicalIndicatorService indicatorService,
        IMarketRegimeClassifier regimeClassifier,
        IEnumerable<IStrategyEvaluator> evaluators,
        PositionSizingCalculator calculator,
        ILogger<BacktestingEngine> logger)
    {
        _upstoxClient = upstoxClient;
        _indicatorService = indicatorService;
        _regimeClassifier = regimeClassifier;
        _evaluators = evaluators;
        _calculator = calculator;
        _logger = logger;
    }

    public async Task<BacktestResultDto> RunBacktestAsync(BacktestRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Backtest for {Key} from {From:yyyy-MM-dd} to {To:yyyy-MM-dd}",
            request.UnderlyingKey, request.FromDate, request.ToDate);

        string fromStr = request.FromDate.ToString("yyyy-MM-dd");
        string toStr = request.ToDate.ToString("yyyy-MM-dd");

        // Fetch Historical Candles across timeframes
        var raw1m = await _upstoxClient.GetHistoricalCandlesAsync(request.UnderlyingKey, "minutes", 1, toStr, fromStr, ct);
        var raw5m = await _upstoxClient.GetHistoricalCandlesAsync(request.UnderlyingKey, "minutes", 5, toStr, fromStr, ct);
        var raw15m = await _upstoxClient.GetHistoricalCandlesAsync(request.UnderlyingKey, "minutes", 15, toStr, fromStr, ct);
        var rawDaily = await _upstoxClient.GetHistoricalCandlesAsync(request.UnderlyingKey, "days", 1, toStr, fromStr, ct);

        var candles1m = raw1m.Select(c => new MarketCandle
        {
            InstrumentKey = request.UnderlyingKey,
            Timeframe = CandleTimeframe.OneMinute,
            CandleTimeUtc = c.Timestamp,
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            Volume = c.Volume,
            IsFinalized = true
        }).OrderBy(c => c.CandleTimeUtc).ToList();

        var candles5m = raw5m.Select(c => new MarketCandle
        {
            InstrumentKey = request.UnderlyingKey,
            Timeframe = CandleTimeframe.FiveMinute,
            CandleTimeUtc = c.Timestamp,
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            Volume = c.Volume,
            IsFinalized = true
        }).OrderBy(c => c.CandleTimeUtc).ToList();

        var candles15m = raw15m.Select(c => new MarketCandle
        {
            InstrumentKey = request.UnderlyingKey,
            Timeframe = CandleTimeframe.FifteenMinute,
            CandleTimeUtc = c.Timestamp,
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            Volume = c.Volume,
            IsFinalized = true
        }).OrderBy(c => c.CandleTimeUtc).ToList();

        var dailyCandles = rawDaily.Select(c => new MarketCandle
        {
            InstrumentKey = request.UnderlyingKey,
            CandleTimeUtc = c.Timestamp,
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            Volume = c.Volume,
            IsFinalized = true
        }).OrderBy(c => c.CandleTimeUtc).ToList();

        return await RunBacktestOnCandlesAsync(candles1m, candles5m, candles15m, dailyCandles, request, ct);
    }

    public Task<BacktestResultDto> RunBacktestOnCandlesAsync(
        IReadOnlyList<MarketCandle> candles1m,
        IReadOnlyList<MarketCandle> candles5m,
        IReadOnlyList<MarketCandle> candles15m,
        IReadOnlyList<MarketCandle> dailyCandles,
        BacktestRequest request,
        CancellationToken ct = default)
    {
        var result = new BacktestResultDto
        {
            UnderlyingKey = request.UnderlyingKey,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            InitialCapital = request.InitialCapital
        };

        if (candles5m.Count == 0)
        {
            return Task.FromResult(result);
        }

        decimal runningCapital = request.InitialCapital;
        decimal dailyLossLimit = Math.Round(request.InitialCapital * (request.MaxDailyLossPercent / 100m), 2);
        decimal peakCapital = runningCapital;
        decimal maxDrawdownAmount = 0m;
        decimal maxDrawdownPercent = 0m;

        int tradeIndex = 1;
        var trades = new List<BacktestTradeDto>();
        var equityCurve = new List<EquityPointDto>
        {
            new() { TimestampUtc = candles5m[0].CandleTimeUtc, Capital = runningCapital, DrawdownAmount = 0m, DrawdownPercent = 0m, TradeNumber = 0 }
        };

        // Group 5-minute candles by IST trading day
        var tradingDays = candles5m
            .GroupBy(c => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(c.CandleTimeUtc, DateTimeKind.Utc), IstZone).Date)
            .OrderBy(g => g.Key)
            .ToList();

        result.TotalTradingDays = tradingDays.Count;

        var dailyPerformances = new List<DailyPerformanceDto>();
        int dailyLossLimitBreaches = 0;

        foreach (var dayGroup in tradingDays)
        {
            DateTime currentDayIst = dayGroup.Key;
            var dayCandles = dayGroup.OrderBy(c => c.CandleTimeUtc).ToList();

            // Find previous day candle for CPR and reference levels
            var prevDailyCandle = dailyCandles
                .Where(c => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(c.CandleTimeUtc, DateTimeKind.Utc), IstZone).Date < currentDayIst)
                .OrderByDescending(c => c.CandleTimeUtc)
                .FirstOrDefault();

            CprResult? dailyCpr = null;
            decimal prevHigh = 0m, prevLow = 0m, prevClose = 0m;
            if (prevDailyCandle != null)
            {
                dailyCpr = _indicatorService.CalculateDailyCpr(prevDailyCandle.High, prevDailyCandle.Low, prevDailyCandle.Close);
                prevHigh = prevDailyCandle.High;
                prevLow = prevDailyCandle.Low;
                prevClose = prevDailyCandle.Close;
            }

            int dailyTradesCount = 0;
            int dailyWins = 0;
            int dailyLosses = 0;
            decimal dailyGrossPnL = 0m;
            decimal dailyNetPnL = 0m;
            int consecutiveLosses = 0;
            bool isDailyHalted = false;

            BacktestTradeDto? activeTrade = null;
            decimal activeDelta = 0.52m;

            for (int i = 0; i < dayCandles.Count; i++)
            {
                var candle = dayCandles[i];
                var candleIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(candle.CandleTimeUtc, DateTimeKind.Utc), IstZone);
                var timeOfDay = candleIst.TimeOfDay;

                // 1. IN-TRADE MANAGEMENT
                if (activeTrade != null)
                {
                    decimal spotDiff = candle.Close - activeTrade.EntryPrice;
                    bool isCall = activeTrade.Direction is SignalDirection.BuyCe or SignalDirection.StrongBuyCe;

                    // Option premium approximation from spot price movement
                    decimal spotMove = isCall ? (candle.Close - activeTrade.EntryPrice) : (activeTrade.EntryPrice - candle.Close);
                    decimal currentPremium = Math.Max(0.50m, Math.Round(activeTrade.EntryPrice + (spotMove * activeDelta), 2));

                    // Evaluate Intra-candle Extremes
                    decimal spotWorstMove = isCall ? (candle.Low - activeTrade.EntryPrice) : (activeTrade.EntryPrice - candle.High);
                    decimal worstPremium = Math.Max(0.50m, Math.Round(activeTrade.EntryPrice + (spotWorstMove * activeDelta), 2));

                    decimal spotBestMove = isCall ? (candle.High - activeTrade.EntryPrice) : (activeTrade.EntryPrice - candle.Low);
                    decimal bestPremium = Math.Max(0.50m, Math.Round(activeTrade.EntryPrice + (spotBestMove * activeDelta), 2));

                    bool shouldExit = false;
                    string exitReason = string.Empty;
                    decimal exitPrice = currentPremium;

                    // Check Stop Loss or Trailing Stop Loss
                    if (worstPremium <= activeTrade.StopLossPrice)
                    {
                        shouldExit = true;
                        exitReason = activeTrade.StopLossPrice > activeTrade.EntryPrice ? "TrailingStopHit" : "StopLossHit";
                        exitPrice = activeTrade.StopLossPrice;
                    }
                    // Check Target Hit
                    else if (bestPremium >= activeTrade.TargetPrice)
                    {
                        shouldExit = true;
                        exitReason = "TargetHit";
                        exitPrice = activeTrade.TargetPrice;
                    }
                    // Check End-of-Day Square Off (at or after 15:15 IST)
                    else if (timeOfDay >= new TimeSpan(15, 15, 0))
                    {
                        shouldExit = true;
                        exitReason = "EndOfDaySquareoff";
                        exitPrice = currentPremium;
                    }
                    // Dynamic Multi-Tier Trailing Stop Loss & Profit Lock (100% Pure Percentage-Based)
                    else if (request.EnableTrailingStop && bestPremium > activeTrade.EntryPrice)
                    {
                        decimal maxGain = bestPremium - activeTrade.EntryPrice;
                        decimal maxRetPct = activeTrade.EntryPrice > 0m ? (maxGain / activeTrade.EntryPrice) * 100m : 0m;
                        decimal proposedSL = activeTrade.StopLossPrice;

                        // Tier 0: Early Risk Reduction (+7% return)
                        if (maxRetPct >= 7.0m)
                        {
                            decimal t0 = Math.Round(activeTrade.EntryPrice * 0.95m, 2);
                            if (t0 > proposedSL) proposedSL = t0;
                        }

                        // Tier 1: Breakeven (+0.5% or +₹1.0 buffer) at +12% return
                        if (maxRetPct >= 12.0m)
                        {
                            decimal be = Math.Round(activeTrade.EntryPrice + Math.Max(1.0m, activeTrade.EntryPrice * 0.005m), 2);
                            if (be > proposedSL) proposedSL = be;
                        }

                        // Tier 2: Lock 40% of Peak Gains at +20% return
                        if (maxRetPct >= 20.0m)
                        {
                            decimal t2 = Math.Round(activeTrade.EntryPrice + (maxGain * 0.40m), 2);
                            if (t2 > proposedSL) proposedSL = t2;
                        }

                        // Tier 3: Lock 60% of Peak Gains at +30% return
                        if (maxRetPct >= 30.0m)
                        {
                            decimal t3 = Math.Round(activeTrade.EntryPrice + (maxGain * 0.60m), 2);
                            if (t3 > proposedSL) proposedSL = t3;
                        }

                        // Tier 4: Super Profit Lock (75% of Peak Gains) at +40% return
                        if (maxRetPct >= 40.0m)
                        {
                            decimal t4 = Math.Round(activeTrade.EntryPrice + (maxGain * 0.75m), 2);
                            if (t4 > proposedSL) proposedSL = t4;
                        }

                        // Tier 5: Chandelier Trail (Max 20% giveback once return >= +15%)
                        if (maxRetPct >= 15.0m)
                        {
                            decimal chan = Math.Round(bestPremium - (maxGain * 0.20m), 2);
                            if (chan > proposedSL) proposedSL = chan;
                        }

                        if (proposedSL > activeTrade.StopLossPrice)
                        {
                            activeTrade.StopLossPrice = proposedSL;
                        }
                    }

                    if (shouldExit)
                    {
                        // Apply exit slippage
                        decimal exitSlippage = request.SimulateSlippage ? Math.Round(exitPrice * (request.SlippagePercent / 100m), 2) : 0m;
                        decimal actualExit = Math.Max(0.10m, exitPrice - exitSlippage);

                        decimal grossPnL = Math.Round((actualExit - activeTrade.EntryPrice) * activeTrade.Quantity, 2);
                        decimal charges = _calculator.CalculateEstimatedRoundTripCharges(
                            activeTrade.EntryPrice, actualExit, activeTrade.Quantity);
                        decimal netPnL = Math.Round(grossPnL - charges, 2);

                        activeTrade.ExitTimeUtc = candle.CandleTimeUtc;
                        activeTrade.ExitPrice = actualExit;
                        activeTrade.GrossPnL = grossPnL;
                        activeTrade.TotalCharges = charges;
                        activeTrade.NetPnL = netPnL;
                        activeTrade.ReturnPercent = activeTrade.EntryPrice > 0m ? Math.Round((netPnL / (activeTrade.EntryPrice * activeTrade.Quantity)) * 100m, 2) : 0m;
                        activeTrade.ExitReason = exitReason;

                        runningCapital += netPnL;
                        activeTrade.RunningCapital = runningCapital;

                        trades.Add(activeTrade);
                        dailyTradesCount++;
                        dailyGrossPnL += grossPnL;
                        dailyNetPnL += netPnL;

                        if (netPnL > 0m)
                        {
                            dailyWins++;
                            consecutiveLosses = 0;
                        }
                        else
                        {
                            dailyLosses++;
                            consecutiveLosses++;
                        }

                        // Drawdown Tracking
                        if (runningCapital > peakCapital)
                        {
                            peakCapital = runningCapital;
                        }
                        decimal ddAmount = peakCapital - runningCapital;
                        decimal ddPercent = peakCapital > 0m ? Math.Round((ddAmount / peakCapital) * 100m, 2) : 0m;

                        if (ddAmount > maxDrawdownAmount)
                        {
                            maxDrawdownAmount = ddAmount;
                            maxDrawdownPercent = ddPercent;
                        }

                        equityCurve.Add(new EquityPointDto
                        {
                            TimestampUtc = candle.CandleTimeUtc,
                            Capital = runningCapital,
                            DrawdownAmount = ddAmount,
                            DrawdownPercent = ddPercent,
                            TradeNumber = tradeIndex++
                        });

                        // Check Daily Circuit Breaker
                        if (dailyNetPnL <= -dailyLossLimit)
                        {
                            isDailyHalted = true;
                            dailyLossLimitBreaches++;
                        }

                        activeTrade = null;
                    }
                }

                // 2. SIGNAL EVALUATION & ENTRY TRIGGER
                if (activeTrade == null && !isDailyHalted)
                {
                    // Check time-of-day constraints (09:30 to 15:10 IST)
                    var sessionStart = new TimeSpan(9, 30, 0);
                    var sessionEnd = new TimeSpan(15, 10, 0);

                    if (timeOfDay >= sessionStart && timeOfDay <= sessionEnd &&
                        dailyTradesCount < request.MaxDailyTrades &&
                        consecutiveLosses < request.MaxConsecutiveLosses)
                    {
                        // Historical context up to current candle
                        var past5m = candles5m.Where(c => c.CandleTimeUtc <= candle.CandleTimeUtc).TakeLast(100).ToList();
                        var past15m = candles15m.Where(c => c.CandleTimeUtc <= candle.CandleTimeUtc).TakeLast(100).ToList();
                        var past1m = candles1m.Where(c => c.CandleTimeUtc <= candle.CandleTimeUtc).TakeLast(100).ToList();

                        if (past5m.Count >= 25)
                        {
                            var mtf = _indicatorService.CalculateMultiTimeframeState(
                                request.UnderlyingKey, past1m, past5m, past15m, dailyCpr);

                            // Build Context
                            var context = new StrategyAnalysisContext
                            {
                                UnderlyingKey = request.UnderlyingKey,
                                CurrentPrice = candle.Close,
                                MultiTimeframeState = mtf,
                                PreviousDayHigh = prevHigh,
                                PreviousDayLow = prevLow,
                                PreviousDayClose = prevClose,
                                EvaluationTimeUtc = candle.CandleTimeUtc
                            };

                            var regime = _regimeClassifier.ClassifyRegime(context);

                            // Strategy Evaluators
                            StrategyEvaluationResult? bestEval = null;
                            decimal highestScore = -1m;

                            foreach (var evaluator in _evaluators)
                            {
                                if (request.TargetStrategy.HasValue && evaluator.Family != request.TargetStrategy.Value)
                                {
                                    continue;
                                }

                                var eval = evaluator.Evaluate(context, regime);
                                decimal score = Math.Max(eval.BullishScore, eval.BearishScore);
                                if (eval.HasSignal && score > highestScore && score >= 65m)
                                {
                                    highestScore = score;
                                    bestEval = eval;
                                }
                            }

                            if (bestEval != null && bestEval.HasSignal)
                            {
                                bool isBullish = bestEval.Direction is SignalDirection.BuyCe or SignalDirection.StrongBuyCe;

                                // Synthetic option premium calculation (ATM options pricing)
                                decimal syntheticEntry = 120.0m;
                                decimal stopDist = Math.Max(20m, bestEval.StopLossUnderlyingDistance);
                                decimal targetDist = bestEval.Target1UnderlyingDistance > 0m ? bestEval.Target1UnderlyingDistance : stopDist * 2m;

                                decimal entrySlippage = request.SimulateSlippage ? Math.Round(syntheticEntry * (request.SlippagePercent / 100m), 2) : 0m;
                                decimal executedEntry = syntheticEntry + entrySlippage;

                                decimal delta = 0.52m;
                                decimal stopLossPremium = Math.Max(1.0m, Math.Round(executedEntry - (stopDist * delta), 2));
                                decimal targetPremium = Math.Round(executedEntry + (targetDist * delta), 2);

                                var (rrValid, _) = _calculator.ValidateRewardToRisk(executedEntry, stopLossPremium, targetPremium, request.MinRewardRiskRatio);

                                if (rrValid)
                                {
                                    int quantity = 65; // 1 lot of NIFTY
                                    decimal requiredCost = executedEntry * quantity;

                                    if (runningCapital >= requiredCost)
                                    {
                                        activeTrade = new BacktestTradeDto
                                        {
                                            TradeNumber = tradeIndex,
                                            EntryTimeUtc = candle.CandleTimeUtc,
                                            InstrumentKey = isBullish ? "NIFTY_ATM_CE" : "NIFTY_ATM_PE",
                                            Direction = bestEval.Direction,
                                            StrategyFamily = bestEval.StrategyFamily,
                                            MarketRegime = regime.Regime,
                                            Quantity = quantity,
                                            EntryPrice = executedEntry,
                                            StopLossPrice = stopLossPremium,
                                            TargetPrice = targetPremium,
                                            RunningCapital = runningCapital
                                        };
                                        activeDelta = delta;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            dailyPerformances.Add(new DailyPerformanceDto
            {
                Date = currentDayIst,
                TradesCount = dailyTradesCount,
                WinningTrades = dailyWins,
                LosingTrades = dailyLosses,
                GrossPnL = dailyGrossPnL,
                NetPnL = dailyNetPnL,
                EndingCapital = runningCapital,
                IsDailyLossHit = isDailyHalted
            });
        }

        // Summary Calculations
        int totalTrades = trades.Count;
        int winCount = trades.Count(t => t.NetPnL > 0m);
        int lossCount = trades.Count(t => t.NetPnL < 0m);
        decimal winRate = totalTrades > 0 ? Math.Round(((decimal)winCount / totalTrades) * 100m, 2) : 0m;

        decimal totalGross = trades.Sum(t => t.GrossPnL);
        decimal totalCharges = trades.Sum(t => t.TotalCharges);
        decimal totalNet = trades.Sum(t => t.NetPnL);

        decimal totalProfit = trades.Where(t => t.NetPnL > 0m).Sum(t => t.NetPnL);
        decimal totalLoss = Math.Abs(trades.Where(t => t.NetPnL < 0m).Sum(t => t.NetPnL));
        decimal profitFactor = totalLoss > 0m ? Math.Round(totalProfit / totalLoss, 2) : (totalProfit > 0m ? 100m : 0m);

        decimal avgWin = winCount > 0 ? Math.Round(totalProfit / winCount, 2) : 0m;
        decimal avgLoss = lossCount > 0 ? Math.Round(totalLoss / lossCount, 2) : 0m;
        decimal winLossRatio = avgLoss > 0m ? Math.Round(avgWin / avgLoss, 2) : 0m;
        decimal expectancy = totalTrades > 0 ? Math.Round(totalNet / totalTrades, 2) : 0m;

        // Sharpe and Sortino Ratios (Session / Daily based)
        var dailyReturns = dailyPerformances.Select(d => d.EndingCapital > 0m ? (double)(d.NetPnL / d.EndingCapital) : 0.0).ToList();
        double meanDailyReturn = dailyReturns.Count > 0 ? dailyReturns.Average() : 0.0;
        double stdDevDaily = dailyReturns.Count > 1 ? Math.Sqrt(dailyReturns.Sum(r => Math.Pow(r - meanDailyReturn, 2)) / (dailyReturns.Count - 1)) : 0.0;
        double downsideDaily = dailyReturns.Where(r => r < 0.0).Count() > 1
            ? Math.Sqrt(dailyReturns.Where(r => r < 0.0).Sum(r => Math.Pow(r, 2)) / dailyReturns.Count)
            : 0.0;

        decimal sharpe = stdDevDaily > 0.0 ? Math.Round((decimal)(meanDailyReturn / stdDevDaily * Math.Sqrt(252)), 2) : 0m;
        decimal sortino = downsideDaily > 0.0 ? Math.Round((decimal)(meanDailyReturn / downsideDaily * Math.Sqrt(252)), 2) : 0m;

        // Strategy Breakdown
        var strategyBreakdowns = trades
            .GroupBy(t => t.StrategyFamily)
            .Select(g =>
            {
                int count = g.Count();
                int wins = g.Count(t => t.NetPnL > 0m);
                decimal net = g.Sum(t => t.NetPnL);
                decimal gross = g.Sum(t => t.GrossPnL);
                decimal pWins = g.Where(t => t.NetPnL > 0m).Sum(t => t.NetPnL);
                decimal pLoss = Math.Abs(g.Where(t => t.NetPnL < 0m).Sum(t => t.NetPnL));
                decimal pf = pLoss > 0m ? Math.Round(pWins / pLoss, 2) : (pWins > 0m ? 100m : 0m);

                return new StrategyBreakdownDto
                {
                    StrategyFamily = g.Key,
                    TotalTrades = count,
                    WinningTrades = wins,
                    WinRatePercent = count > 0 ? Math.Round(((decimal)wins / count) * 100m, 2) : 0m,
                    GrossPnL = gross,
                    NetPnL = net,
                    ProfitFactor = pf
                };
            }).ToList();

        // Regime Breakdown
        var regimeBreakdowns = trades
            .GroupBy(t => t.MarketRegime)
            .Select(g =>
            {
                int count = g.Count();
                int wins = g.Count(t => t.NetPnL > 0m);
                decimal net = g.Sum(t => t.NetPnL);

                return new RegimeBreakdownDto
                {
                    Regime = g.Key,
                    TotalTrades = count,
                    WinningTrades = wins,
                    WinRatePercent = count > 0 ? Math.Round(((decimal)wins / count) * 100m, 2) : 0m,
                    NetPnL = net
                };
            }).ToList();

        result.FinalCapital = runningCapital;
        result.TotalGrossPnL = totalGross;
        result.TotalCharges = totalCharges;
        result.TotalNetPnL = totalNet;
        result.TotalReturnPercent = request.InitialCapital > 0m ? Math.Round((totalNet / request.InitialCapital) * 100m, 2) : 0m;
        result.TotalTrades = totalTrades;
        result.WinningTrades = winCount;
        result.LosingTrades = lossCount;
        result.WinRatePercent = winRate;
        result.ProfitFactor = profitFactor;
        result.AverageTradePnL = expectancy;
        result.AverageWin = avgWin;
        result.AverageLoss = avgLoss;
        result.WinLossRatio = winLossRatio;
        result.MathematicalExpectancy = expectancy;
        result.MaxDrawdownAmount = maxDrawdownAmount;
        result.MaxDrawdownPercent = maxDrawdownPercent;
        result.SharpeRatio = sharpe;
        result.SortinoRatio = sortino;
        result.DailyLossLimitBreaches = dailyLossLimitBreaches;

        result.Trades = trades;
        result.DailyPerformance = dailyPerformances;
        result.EquityCurve = equityCurve;
        result.StrategyBreakdowns = strategyBreakdowns;
        result.RegimeBreakdowns = regimeBreakdowns;

        _logger.LogInformation("Backtest Completed: Trades={Total}, WinRate={WR}%, NetPnL=₹{Net}, Drawdown={DD}%",
            totalTrades, winRate, totalNet, maxDrawdownPercent);

        return Task.FromResult(result);
    }

    private static TimeZoneInfo GetIstTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
    }
}
