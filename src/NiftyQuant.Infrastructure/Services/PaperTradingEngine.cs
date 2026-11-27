using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.PaperTrading;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Application.Services.Risk;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;

namespace NiftyQuant.Infrastructure.Services;

public class PaperTradingEngine : IPaperTradingEngine
{
    private static readonly TimeZoneInfo IstZone = GetIstTimeZone();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRiskManagementService _riskService;
    private readonly PositionSizingCalculator _calculator;
    private readonly RiskOptions _riskOptions;
    private readonly ILogger<PaperTradingEngine> _logger;

    public event Func<PaperOrder, Task>? OnOrderPlaced;
    public event Func<PaperOrder, Task>? OnOrderExecuted;
    public event Func<PaperPosition, Task>? OnPositionOpened;
    public event Func<PaperPosition, Task>? OnPositionUpdated;
    public event Func<PaperTrade, Task>? OnPositionClosed;

    public PaperTradingEngine(
        IServiceScopeFactory scopeFactory,
        IRiskManagementService riskService,
        PositionSizingCalculator calculator,
        IOptions<RiskOptions> riskOptions,
        ILogger<PaperTradingEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _riskService = riskService;
        _calculator = calculator;
        _riskOptions = riskOptions.Value;
        _logger = logger;
    }

    public async Task<OrderExecutionResult> ExecuteSignalAsync(
        StrategySignal signal,
        bool simulateSlippage = true,
        CancellationToken ct = default)
    {
        // 1. Pre-Trade Risk Management Hard Check
        var riskValidation = await _riskService.ValidatePreTradeAsync(signal, ct);
        if (!riskValidation.IsApproved)
        {
            string rejectReason = string.Join("; ", riskValidation.VetoReasons);
            _logger.LogWarning("Pre-Trade Risk Rejected for signal {SignalId}: {Reason}", signal.Id, rejectReason);

            // Record rejected order in database
            var rejectedOrder = new PaperOrder
            {
                Id = Guid.NewGuid(),
                SignalId = signal.Id,
                InstrumentKey = signal.SelectedInstrumentKey,
                Side = PaperOrderSide.Buy,
                Quantity = riskValidation.AllocatedQuantity > 0 ? riskValidation.AllocatedQuantity : IndexRegistry.GetProfile(signal.UnderlyingKey).LotSize,
                OrderedPrice = signal.EntryPremium,
                ExecutedPrice = 0m,
                Slippage = 0m,
                EstimatedCharges = 0m,
                Status = PaperOrderStatus.Rejected,
                RejectReason = rejectReason,
                OrderTimeUtc = DateTime.UtcNow
            };

            await SaveOrderAsync(rejectedOrder, ct);
            if (OnOrderPlaced != null) await OnOrderPlaced.Invoke(rejectedOrder);

            return new OrderExecutionResult
            {
                IsSuccess = false,
                OrderId = rejectedOrder.Id,
                InstrumentKey = signal.SelectedInstrumentKey,
                Side = PaperOrderSide.Buy,
                Quantity = rejectedOrder.Quantity,
                OrderedPrice = signal.EntryPremium,
                Status = PaperOrderStatus.Rejected,
                Message = $"Order rejected by Risk Management: {rejectReason}"
            };
        }

        // 2. Realistic Execution & Slippage Modeling
        decimal slippage = 0m;
        if (simulateSlippage)
        {
            decimal slippagePercent = _riskOptions.SlippagePerOrderPercent > 0m ? _riskOptions.SlippagePerOrderPercent : 0.2m;
            slippage = Math.Round(signal.EntryPremium * (slippagePercent / 100m), 2);
        }

        decimal executedPrice = Math.Round(signal.EntryPremium + slippage, 2);
        int quantity = riskValidation.AllocatedQuantity;
        decimal charges = _calculator.CalculateEstimatedRoundTripCharges(
            executedPrice, executedPrice, quantity, _riskOptions.BrokeragePerOrder);

        // 3. Create and Save PaperOrder
        string tradingSymbol = !string.IsNullOrEmpty(signal.SelectedInstrumentKey) && signal.SelectedStrike > 0
            ? $"NIFTY {signal.SelectedExpiry:dd MMM} {signal.SelectedStrike:0} {signal.SelectedOptionType}".ToUpperInvariant()
            : (signal.Direction.ToString().Contains("Ce") ? "NIFTY CE" : "NIFTY PE");

        var order = new PaperOrder
        {
            Id = Guid.NewGuid(),
            SignalId = signal.Id,
            InstrumentKey = signal.SelectedInstrumentKey,
            TradingSymbol = tradingSymbol,
            Side = PaperOrderSide.Buy,
            OptionType = signal.SelectedOptionType,
            StrikePrice = signal.SelectedStrike,
            Quantity = quantity,
            OrderedPrice = signal.EntryPremium,
            ExecutedPrice = executedPrice,
            Slippage = slippage,
            EstimatedCharges = charges,
            Status = PaperOrderStatus.Filled,
            OrderTimeUtc = DateTime.UtcNow,
            ExecutedTimeUtc = DateTime.UtcNow
        };

        // 4. Create and Save PaperPosition with Strict Intraday Risk Bounds
        // For option buying, technical risk must NEVER exceed 15% of premium (or 25 pts max) to prevent oversized drawdowns
        decimal technicalRisk = (signal.StopLossPremium > 0m && signal.StopLossPremium < executedPrice)
            ? (executedPrice - signal.StopLossPremium)
            : Math.Clamp(executedPrice * 0.12m, 12m, 20m);

        // Strict clamp: Min 8% / 8 pts, Max 15% / 25 pts (Max loss capped at ~₹1,200 - ₹1,600 per lot)
        decimal boundedRisk = Math.Clamp(technicalRisk, Math.Max(8m, executedPrice * 0.08m), Math.Min(25m, Math.Max(15m, executedPrice * 0.15m)));

        decimal initialStopLoss = Math.Round(executedPrice - boundedRisk, 2);

        decimal target1 = signal.Target1Premium > (executedPrice + 5m)
            ? signal.Target1Premium
            : Math.Round(executedPrice + (boundedRisk * 2.0m), 2);

        var position = new PaperPosition
        {
            Id = Guid.NewGuid(),
            InstrumentKey = signal.SelectedInstrumentKey,
            TradingSymbol = tradingSymbol,
            OptionType = signal.SelectedOptionType,
            StrikePrice = signal.SelectedStrike,
            ExpiryDate = signal.SelectedExpiry,
            Quantity = quantity,
            AverageBuyPrice = executedPrice,
            CurrentLtp = executedPrice,
            StopLossPrice = initialStopLoss,
            TargetPrice = target1,
            TrailingStopLossPrice = initialStopLoss,
            MaxLtpReached = executedPrice,
            UnrealizedPnL = 0m,
            IsOpen = true,
            OpenedAtUtc = DateTime.UtcNow
        };

        // 5. Update Account Margin in Database
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var account = await db.PaperAccounts.FirstAsync(ct);
            decimal marginCost = executedPrice * quantity;
            account.UtilizedMargin += marginCost;
            account.AvailableBalance -= marginCost;
            account.UpdatedAtUtc = DateTime.UtcNow;

            db.PaperOrders.Add(order);
            db.PaperPositions.Add(position);

            // Mark strategy signal as executed
            var dbSignal = await db.StrategySignals.FirstOrDefaultAsync(s => s.Id == signal.Id, ct);
            if (dbSignal != null)
            {
                dbSignal.IsExecuted = true;
            }

            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Paper Order Executed: Instrument={Key}, Qty={Qty}, Price=₹{Price}, Slippage=₹{Slippage}",
            order.InstrumentKey, quantity, executedPrice, slippage);

        // Fire lifecycle events
        if (OnOrderPlaced != null) await OnOrderPlaced.Invoke(order);
        if (OnOrderExecuted != null) await OnOrderExecuted.Invoke(order);
        if (OnPositionOpened != null) await OnPositionOpened.Invoke(position);

        return new OrderExecutionResult
        {
            IsSuccess = true,
            OrderId = order.Id,
            PositionId = position.Id,
            InstrumentKey = order.InstrumentKey,
            TradingSymbol = order.TradingSymbol,
            Side = PaperOrderSide.Buy,
            OptionType = signal.SelectedOptionType,
            StrikePrice = signal.SelectedStrike,
            Quantity = quantity,
            OrderedPrice = signal.EntryPremium,
            ExecutedPrice = executedPrice,
            StopLossPrice = position.StopLossPrice,
            TargetPrice = position.TargetPrice,
            Slippage = slippage,
            Charges = charges,
            Status = PaperOrderStatus.Filled,
            Message = "Paper trade order executed successfully."
        };
    }

    public async Task<ClosePositionResult> ClosePositionAsync(
        Guid positionId,
        string exitReason = "ManualClose",
        decimal? exitPrice = null,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();

        var position = await db.PaperPositions.FirstOrDefaultAsync(p => p.Id == positionId && p.IsOpen, ct);
        if (position == null)
        {
            return new ClosePositionResult
            {
                IsSuccess = false,
                PositionId = positionId,
                Message = $"Open position with ID {positionId} not found."
            };
        }

        decimal rawPrice = exitPrice ?? position.CurrentLtp;
        if (rawPrice <= 0m) rawPrice = position.AverageBuyPrice;

        // Apply exit selling slippage (0.2%)
        decimal slippage = Math.Round(rawPrice * (_riskOptions.SlippagePerOrderPercent / 100m), 2);
        decimal actualExitPrice = Math.Max(0.05m, Math.Round(rawPrice - slippage, 2));

        decimal grossPnL = Math.Round((actualExitPrice - position.AverageBuyPrice) * position.Quantity, 2);
        decimal totalCharges = _calculator.CalculateEstimatedRoundTripCharges(
            position.AverageBuyPrice, actualExitPrice, position.Quantity, _riskOptions.BrokeragePerOrder);
        decimal netPnL = Math.Round(grossPnL - totalCharges, 2);
        decimal returnPercent = position.AverageBuyPrice > 0m
            ? Math.Round(((actualExitPrice - position.AverageBuyPrice) / position.AverageBuyPrice) * 100m, 2)
            : 0m;
        double durationMinutes = Math.Round((DateTime.UtcNow - position.OpenedAtUtc).TotalMinutes, 1);

        // Close position
        position.IsOpen = false;
        position.ClosedAtUtc = DateTime.UtcNow;
        position.CurrentLtp = actualExitPrice;
        position.UnrealizedPnL = 0m;

        // Create Sell Order
        var sellOrder = new PaperOrder
        {
            Id = Guid.NewGuid(),
            InstrumentKey = position.InstrumentKey,
            TradingSymbol = position.TradingSymbol ?? string.Empty,
            Side = PaperOrderSide.Sell,
            OptionType = position.OptionType,
            StrikePrice = position.StrikePrice,
            Quantity = position.Quantity,
            OrderedPrice = rawPrice,
            ExecutedPrice = actualExitPrice,
            Slippage = slippage,
            EstimatedCharges = Math.Round(totalCharges / 2m, 2),
            Status = PaperOrderStatus.Filled,
            OrderTimeUtc = DateTime.UtcNow,
            ExecutedTimeUtc = DateTime.UtcNow
        };
        db.PaperOrders.Add(sellOrder);

        // Create Completed Trade Ledger
        var trade = new PaperTrade
        {
            Id = Guid.NewGuid(),
            InstrumentKey = position.InstrumentKey,
            TradingSymbol = position.TradingSymbol ?? string.Empty,
            OptionType = position.OptionType,
            StrikePrice = position.StrikePrice,
            ExpiryDate = position.ExpiryDate,
            Quantity = position.Quantity,
            EntryPrice = position.AverageBuyPrice,
            ExitPrice = actualExitPrice,
            SlippagePaid = slippage,
            TotalCharges = totalCharges,
            GrossPnL = grossPnL,
            NetPnL = netPnL,
            ReturnPercent = returnPercent,
            DurationMinutes = durationMinutes,
            EntryTimeUtc = position.OpenedAtUtc,
            ExitTimeUtc = DateTime.UtcNow,
            ExitReason = exitReason,
            Status = PaperTradeStatus.Closed
        };
        db.PaperTrades.Add(trade);

        // Restore Margin to Account
        var account = await db.PaperAccounts.FirstAsync(ct);
        decimal originalMargin = position.AverageBuyPrice * position.Quantity;
        account.UtilizedMargin = Math.Max(0m, account.UtilizedMargin - originalMargin);
        account.AvailableBalance += originalMargin;
        account.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        // Notify Risk Management of completed trade result
        await _riskService.RecordTradeResultAsync(trade, ct);

        _logger.LogInformation("Closed Paper Position {PositionId} ({Reason}): Gross=₹{Gross}, Net=₹{Net}, Charges=₹{Charges}",
            positionId, exitReason, grossPnL, netPnL, totalCharges);

        if (OnOrderExecuted != null) await OnOrderExecuted.Invoke(sellOrder);
        if (OnPositionClosed != null) await OnPositionClosed.Invoke(trade);

        return new ClosePositionResult
        {
            IsSuccess = true,
            TradeId = trade.Id,
            PositionId = position.Id,
            InstrumentKey = position.InstrumentKey,
            TradingSymbol = position.TradingSymbol ?? string.Empty,
            EntryPrice = position.AverageBuyPrice,
            ExitPrice = actualExitPrice,
            Quantity = position.Quantity,
            GrossPnL = grossPnL,
            TotalCharges = totalCharges,
            NetPnL = netPnL,
            ExitReason = exitReason,
            Message = $"Position closed successfully. Net PnL: ₹{netPnL:F2}."
        };
    }

    public async Task<List<ClosePositionResult>> SquareOffAllPositionsAsync(
        string exitReason = "EndOfDaySquareoff",
        CancellationToken ct = default)
    {
        var results = new List<ClosePositionResult>();
        var openPositions = await GetOpenPositionsAsync(ct);

        foreach (var pos in openPositions)
        {
            try
            {
                var res = await ClosePositionAsync(pos.Id, exitReason, pos.CurrentLtp, ct);
                results.Add(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to square off position {PositionId}", pos.Id);
            }
        }

        return results;
    }

    public async Task UpdatePositionPricesAsync(
        string instrumentKey,
        decimal currentLtp,
        CancellationToken ct = default)
    {
        if (currentLtp <= 0m) return;

        List<Guid> positionsToCloseSl = new();
        List<Guid> positionsToCloseTsl = new();
        List<Guid> positionsToCloseTgt = new();

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();

            var positions = await db.PaperPositions
                .Where(p => p.IsOpen && p.InstrumentKey == instrumentKey)
                .ToListAsync(ct);

            if (positions.Count == 0) return;

            foreach (var pos in positions)
            {
                pos.CurrentLtp = currentLtp;
                pos.UnrealizedPnL = Math.Round((currentLtp - pos.AverageBuyPrice) * pos.Quantity, 2);

                if (currentLtp > pos.MaxLtpReached)
                {
                    pos.MaxLtpReached = currentLtp;
                }

                decimal maxGainPerShare = pos.MaxLtpReached - pos.AverageBuyPrice;
                decimal maxProfitAmount = Math.Round(maxGainPerShare * pos.Quantity, 2);
                decimal maxReturnPercent = pos.AverageBuyPrice > 0m
                    ? Math.Round((maxGainPerShare / pos.AverageBuyPrice) * 100m, 2)
                    : 0m;

                // 1. Dynamic Multi-Tier Trailing Stop Loss & Profit Securing Engine (100% Pure Percentage-Based)
                if (_riskOptions.EnableTrailingStopLoss && maxGainPerShare > 0m)
                {
                    decimal proposedSL = pos.StopLossPrice;
                    decimal initialRisk = pos.AverageBuyPrice - pos.StopLossPrice;

                    // Tier 0: Early Risk Reduction (+10% return reached)
                    // If trade is in profit (+10%), trim remaining risk to at most -8% (avoids choking early pullbacks)
                    if (maxReturnPercent >= 10.0m)
                    {
                        decimal tier0SL = Math.Round(pos.AverageBuyPrice * 0.92m, 2);
                        if (tier0SL > proposedSL)
                        {
                            proposedSL = tier0SL;
                        }
                    }

                    // Tier 1: Capital Preservation & Breakeven Lock (+15% return OR 1.5R reached with min 20 pts)
                    // Gives trade sufficient room to absorb post-breakout pullback before locking breakeven
                    if (maxReturnPercent >= 15.0m || (initialRisk > 0m && maxGainPerShare >= (initialRisk * 1.5m) && maxGainPerShare >= 20m))
                    {
                        decimal beLevel = Math.Round(pos.AverageBuyPrice + Math.Max(1.0m, pos.AverageBuyPrice * 0.005m), 2);
                        if (beLevel > proposedSL)
                        {
                            proposedSL = beLevel;
                        }
                    }

                    // Tier 2: Lock 40% of Peak Gains (+20% return)
                    if (maxReturnPercent >= 20.0m)
                    {
                        decimal tier2Level = Math.Round(pos.AverageBuyPrice + (maxGainPerShare * 0.40m), 2);
                        if (tier2Level > proposedSL)
                        {
                            proposedSL = tier2Level;
                        }
                    }

                    // Tier 3: Lock 60% of Peak Gains (+30% return)
                    if (maxReturnPercent >= 30.0m)
                    {
                        decimal tier3Level = Math.Round(pos.AverageBuyPrice + (maxGainPerShare * 0.60m), 2);
                        if (tier3Level > proposedSL)
                        {
                            proposedSL = tier3Level;
                        }
                    }

                    // Tier 4: Super Profit Lock (75% of Peak Gains) (+40% return)
                    if (maxReturnPercent >= 40.0m)
                    {
                        decimal tier4Level = Math.Round(pos.AverageBuyPrice + (maxGainPerShare * 0.75m), 2);
                        if (tier4Level > proposedSL)
                        {
                            proposedSL = tier4Level;
                        }
                    }

                    // Tier 5: Chandelier High-Water Mark Trail (Max 20% giveback once return >= +15%)
                    if (maxReturnPercent >= 15.0m)
                    {
                        decimal chandelierFloor = Math.Round(pos.MaxLtpReached - (maxGainPerShare * 0.20m), 2);
                        if (chandelierFloor > proposedSL)
                        {
                            proposedSL = chandelierFloor;
                        }
                    }

                    // Ratchet SL Upwards Only
                    if (proposedSL > pos.StopLossPrice)
                    {
                        _logger.LogInformation("[PROFIT LOCK / TSL] Ratcheted Stop Loss for {Symbol}: Old SL=₹{OldSL:F2} -> New SL=₹{NewSL:F2} (Peak LTP=₹{Peak:F2}, Max Return=+{MaxRet:F1}%, Max Profit=₹{MaxProfit:F2})",
                            pos.TradingSymbol, pos.StopLossPrice, proposedSL, pos.MaxLtpReached, maxReturnPercent, maxProfitAmount);

                        pos.StopLossPrice = proposedSL;
                        pos.TrailingStopLossPrice = proposedSL;
                    }
                }

                // 2. Target Hit Check
                if (currentLtp >= pos.TargetPrice)
                {
                    _logger.LogInformation("Target hit for {Symbol}! Current=₹{Ltp:F2}, Target=₹{Tgt:F2}", 
                        !string.IsNullOrEmpty(pos.TradingSymbol) ? pos.TradingSymbol : instrumentKey, 
                        currentLtp, pos.TargetPrice);
                    positionsToCloseTgt.Add(pos.Id);
                    continue;
                }

                // 3. Stop Loss / Trailing Stop Loss Hit Check
                if (currentLtp <= pos.StopLossPrice)
                {
                    if (pos.StopLossPrice > pos.AverageBuyPrice)
                    {
                        _logger.LogInformation("[TRAILING STOP HIT] Exiting {Symbol} in PROFIT! Current=₹{Ltp:F2}, Locked SL=₹{SL:F2}, Entry=₹{Entry:F2}, Locked Profit=₹{PnL:F2}",
                            pos.TradingSymbol, currentLtp, pos.StopLossPrice, pos.AverageBuyPrice, pos.UnrealizedPnL);
                        positionsToCloseTsl.Add(pos.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Stop loss hit for {Symbol}! Current=₹{Ltp:F2}, SL=₹{SL:F2}", 
                            !string.IsNullOrEmpty(pos.TradingSymbol) ? pos.TradingSymbol : instrumentKey, 
                            currentLtp, pos.StopLossPrice);
                        positionsToCloseSl.Add(pos.Id);
                    }
                    continue;
                }

                if (OnPositionUpdated != null)
                {
                    await OnPositionUpdated.Invoke(pos);
                }
            }

            await db.SaveChangesAsync(ct);
        }

        // Close positions that hit SL, TSL or Target
        foreach (var id in positionsToCloseSl)
        {
            await ClosePositionAsync(id, "StopLossHit", currentLtp, ct);
        }

        foreach (var id in positionsToCloseTsl)
        {
            await ClosePositionAsync(id, "TrailingStopHit", currentLtp, ct);
        }

        foreach (var id in positionsToCloseTgt)
        {
            await ClosePositionAsync(id, "TargetHit", currentLtp, ct);
        }
    }

    public async Task<IReadOnlyList<PaperPosition>> GetOpenPositionsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
        return await db.PaperPositions.Where(p => p.IsOpen).OrderByDescending(p => p.OpenedAtUtc).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaperTrade>> GetTradeHistoryAsync(int limit = 50, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
        return await db.PaperTrades.OrderByDescending(t => t.ExitTimeUtc).Take(limit).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaperOrder>> GetOrderHistoryAsync(int limit = 50, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
        return await db.PaperOrders.OrderByDescending(o => o.OrderTimeUtc).Take(limit).ToListAsync(ct);
    }

    public async Task<PaperPortfolioSummaryDto> GetPortfolioSummaryAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();

        var account = await db.PaperAccounts.FirstOrDefaultAsync(ct);
        var trades = await db.PaperTrades.OrderBy(t => t.ExitTimeUtc).ToListAsync(ct);
        var openPositions = await db.PaperPositions.Where(p => p.IsOpen).ToListAsync(ct);

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

        decimal avgProfit = winCount > 0 ? Math.Round(totalProfit / winCount, 2) : 0m;
        decimal avgLoss = lossCount > 0 ? Math.Round(totalLoss / lossCount, 2) : 0m;
        decimal expectancy = totalTrades > 0 ? Math.Round(totalNet / totalTrades, 2) : 0m;

        // Drawdown Calculation
        decimal peakCapital = account?.InitialCapital ?? 50000m;
        decimal runningCapital = peakCapital;
        decimal maxDrawdownAmount = 0m;
        decimal maxDrawdownPercent = 0m;

        foreach (var t in trades)
        {
            runningCapital += t.NetPnL;
            if (runningCapital > peakCapital)
            {
                peakCapital = runningCapital;
            }
            decimal dd = peakCapital - runningCapital;
            if (dd > maxDrawdownAmount)
            {
                maxDrawdownAmount = dd;
                maxDrawdownPercent = Math.Round((dd / peakCapital) * 100m, 2);
            }
        }

        return new PaperPortfolioSummaryDto
        {
            TotalTrades = totalTrades,
            WinningTrades = winCount,
            LosingTrades = lossCount,
            WinRatePercent = winRate,
            TotalGrossPnL = totalGross,
            TotalChargesPaid = totalCharges,
            TotalNetPnL = totalNet,
            ProfitFactor = profitFactor,
            AverageProfitPerTrade = avgProfit,
            AverageLossPerTrade = avgLoss,
            Expectancy = expectancy,
            MaxDrawdownAmount = maxDrawdownAmount,
            MaxDrawdownPercent = maxDrawdownPercent,
            CurrentCapital = (account?.AvailableBalance ?? 50000m) + (account?.UtilizedMargin ?? 0m),
            AvailableBalance = account?.AvailableBalance ?? 50000m,
            ActiveOpenPositionsCount = openPositions.Count,
            ActiveUnrealizedPnL = openPositions.Sum(p => p.UnrealizedPnL)
        };
    }

    private async Task SaveOrderAsync(PaperOrder order, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
        db.PaperOrders.Add(order);
        await db.SaveChangesAsync(ct);
    }

    private static TimeZoneInfo GetIstTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
    }
}
