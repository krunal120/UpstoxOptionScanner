using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Risk;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Application.Services.Risk;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;

namespace NiftyQuant.Infrastructure.Services;

public class RiskManagementService : IRiskManagementService
{
    private static readonly TimeZoneInfo IstZone = GetIstTimeZone();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PositionSizingCalculator _calculator;
    private readonly RiskOptions _riskOptions;
    private readonly ILogger<RiskManagementService> _logger;

    public RiskManagementService(
        IServiceScopeFactory scopeFactory,
        PositionSizingCalculator calculator,
        IOptions<RiskOptions> riskOptions,
        ILogger<RiskManagementService> logger)
    {
        _scopeFactory = scopeFactory;
        _calculator = calculator;
        _riskOptions = riskOptions.Value;
        _logger = logger;
    }

    public async Task<PreTradeRiskValidationResult> ValidatePreTradeAsync(
        StrategySignal signal,
        CancellationToken ct = default)
    {
        var profile = IndexRegistry.GetProfile(signal?.UnderlyingKey);
        var result = new PreTradeRiskValidationResult
        {
            LotSize = profile.LotSize,
            ExecutionMode = "SimulatedPaperTradingOnly"
        };

        // 1. Hard Rule: Strict Read-Only / Paper Trading Protection
        // Real-money order routing is strictly disabled in Version 1.
        result.ExecutionMode = "SimulatedPaperTradingOnly";

        // 2. Signal Validity Check
        if (signal == null)
        {
            result.VetoReasons.Add("Signal is null.");
            result.IsApproved = false;
            return result;
        }

        bool isExecutableDirection = signal.Direction is SignalDirection.BuyCe
            or SignalDirection.StrongBuyCe
            or SignalDirection.BuyPe
            or SignalDirection.StrongBuyPe;

        if (!isExecutableDirection)
        {
            result.VetoReasons.Add($"Signal direction '{signal.Direction}' is not an executable options buying trigger.");
            result.IsApproved = false;
            return result;
        }

        if (string.IsNullOrWhiteSpace(signal.SelectedInstrumentKey))
        {
            result.VetoReasons.Add("Signal does not contain a selected option contract instrument key.");
            result.IsApproved = false;
            return result;
        }

        if (signal.EntryPremium <= 0m || signal.StopLossPremium <= 0m)
        {
            result.VetoReasons.Add($"Invalid premiums: Entry=₹{signal.EntryPremium}, StopLoss=₹{signal.StopLossPremium}.");
            result.IsApproved = false;
            return result;
        }

        if (signal.StopLossPremium >= signal.EntryPremium)
        {
            result.VetoReasons.Add($"Stop-loss premium (₹{signal.StopLossPremium}) must be below entry premium (₹{signal.EntryPremium}).");
            result.IsApproved = false;
            return result;
        }

        // 3. Time-of-Day Veto (IST market session: 09:30 to 15:10, bypassed only for manual test-execute signals)
        var signalTime = signal.SignalTimeUtc != default ? signal.SignalTimeUtc : DateTime.UtcNow;
        var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(signalTime, DateTimeKind.Utc), IstZone);
        var sessionStart = new TimeSpan(9, 30, 0);
        var sessionEnd = new TimeSpan(15, 10, 0);
        bool isTestSignal = signal.Reasons.Any(r => string.Equals(r.Category, "Manual/Test", StringComparison.OrdinalIgnoreCase));

        if (!isTestSignal && (istNow.TimeOfDay < sessionStart || istNow.TimeOfDay > sessionEnd))
        {
            result.VetoReasons.Add($"Time-of-day veto: Current IST time {istNow:HH:mm} is outside the active trading window (09:30 - 15:10).");
            result.IsApproved = false;
            return result;
        }

        // 4. Prohibition of Averaging Down / Martingale (Strict Rule)
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
            var existingPosition = await db.PaperPositions
                .FirstOrDefaultAsync(p => p.IsOpen && p.InstrumentKey == signal.SelectedInstrumentKey, ct);

            if (existingPosition != null)
            {
                result.VetoReasons.Add($"Strict Rule: Cannot add to or average down on an already open position for {signal.SelectedInstrumentKey}.");
                result.IsApproved = false;
                return result;
            }
        }

        // 5. Account & Daily Risk Status Verification
        var dailyStatus = await GetDailyRiskStatusAsync(ct);

        if (dailyStatus.IsCircuitBreakerTripped)
        {
            result.VetoReasons.Add($"Circuit breaker tripped: {dailyStatus.CircuitBreakerReason}");
            result.IsApproved = false;
            return result;
        }

        if (dailyStatus.ActivePositionsCount >= _riskOptions.MaxConcurrentPositions)
        {
            result.VetoReasons.Add($"Maximum concurrent positions limit ({_riskOptions.MaxConcurrentPositions}) reached. Active positions: {dailyStatus.ActivePositionsCount}.");
            result.IsApproved = false;
            return result;
        }

        if (dailyStatus.TradesCountToday >= _riskOptions.MaxDailyTrades)
        {
            result.VetoReasons.Add($"Maximum daily trades limit ({_riskOptions.MaxDailyTrades}) reached for today.");
            result.IsApproved = false;
            return result;
        }

        if (dailyStatus.ConsecutiveLossesToday >= _riskOptions.MaxConsecutiveLosses)
        {
            result.VetoReasons.Add($"Consecutive loss circuit breaker tripped: {dailyStatus.ConsecutiveLossesToday} consecutive losses recorded today.");
            result.IsApproved = false;
            return result;
        }

        // 6. Minimum Reward-to-Risk Target Check (Minimum 1:2)
        var targetPremium = signal.Target1Premium > signal.EntryPremium ? signal.Target1Premium : signal.EntryPremium + ((signal.EntryPremium - signal.StopLossPremium) * 2m);
        var (rrValid, rrRatio) = _calculator.ValidateRewardToRisk(
            signal.EntryPremium, signal.StopLossPremium, targetPremium, _riskOptions.MinRewardRiskRatio);

        result.RewardToRiskRatio = rrRatio;
        if (!rrValid)
        {
            result.VetoReasons.Add($"Reward-to-risk ratio 1:{rrRatio:F2} is below the mandatory minimum of 1:{_riskOptions.MinRewardRiskRatio:F1}.");
            result.IsApproved = false;
            return result;
        }

        // 7. Position Sizing & Margin Adequacy
        var account = await GetOrCreateDefaultAccountAsync(ct);
        var sizing = _calculator.CalculatePositionSize(
            signal.EntryPremium,
            signal.StopLossPremium,
            account.AvailableBalance,
            account.MaxRiskPerTrade,
            profile.LotSize,
            _riskOptions.MaxConcurrentPositions);

        if (!sizing.IsAffordable)
        {
            result.VetoReasons.Add(sizing.RejectionReason);
            result.IsApproved = false;
            return result;
        }

        // Check if 1 lot risk exceeds max allowable trade risk
        decimal maxAllowedRisk = account.MaxRiskPerTrade * 1.5m; // 50% buffer for 1 lot index granularity
        if (sizing.TotalRiskAmount > maxAllowedRisk)
        {
            result.Warnings.Add($"Per-lot risk of ₹{sizing.TotalRiskAmount:F2} exceeds base risk budget ₹{account.MaxRiskPerTrade:F2}. Stop loss is relatively wide.");
        }

        result.IsApproved = true;
        result.AllocatedLots = sizing.AllocatedLots;
        result.AllocatedQuantity = sizing.AllocatedQuantity;
        result.RequiredMargin = sizing.TotalCost;
        result.ExpectedRiskAmount = sizing.TotalRiskAmount;
        result.MaxLossAllowed = account.MaxRiskPerTrade;

        _logger.LogInformation("Pre-Trade Risk Approved: Instrument={Key}, Lots={Lots}, Margin=₹{Margin}, Risk=₹{Risk}, R:R=1:{RR}",
            signal.SelectedInstrumentKey, result.AllocatedLots, result.RequiredMargin, result.ExpectedRiskAmount, result.RewardToRiskRatio);

        return result;
    }

    public async Task<DailyRiskStatusDto> GetDailyRiskStatusAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();

        var account = await GetOrCreateAccountInternalAsync(db, ct);

        // Compute IST date boundaries
        var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
        var istTodayStart = istNow.Date;
        var utcTodayStart = TimeZoneInfo.ConvertTimeToUtc(istTodayStart, IstZone);

        // 1. Active positions & unrealized PnL
        var activePositions = await db.PaperPositions
            .Where(p => p.IsOpen)
            .ToListAsync(ct);

        decimal unrealizedPnL = activePositions.Sum(p => p.UnrealizedPnL);
        int activeCount = activePositions.Count;

        // 2. Closed trades today & realized PnL
        var closedTradesToday = await db.PaperTrades
            .Where(t => t.ExitTimeUtc >= utcTodayStart)
            .OrderBy(t => t.ExitTimeUtc)
            .ToListAsync(ct);

        decimal realizedPnLToday = closedTradesToday.Sum(t => t.NetPnL);
        int tradesCountToday = closedTradesToday.Count;

        // 3. Consecutive losses today (from latest backwards)
        int consecutiveLosses = 0;
        for (int i = closedTradesToday.Count - 1; i >= 0; i--)
        {
            if (closedTradesToday[i].NetPnL < 0m)
            {
                consecutiveLosses++;
            }
            else
            {
                break;
            }
        }

        decimal totalPnLToday = realizedPnLToday + unrealizedPnL;
        decimal dailyLossLimit = account.DailyLossLimit;
        decimal dailyLossRemaining = Math.Max(0m, dailyLossLimit + totalPnLToday);

        // Auto-unlock account if date rolled over to a new trading session
        if (account.IsTradingLockedForDay && account.UpdatedAtUtc < utcTodayStart)
        {
            account.IsTradingLockedForDay = false;
            account.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        // 4. Circuit Breaker Conditions
        bool isTripped = false;
        string tripReason = string.Empty;

        if (totalPnLToday <= -dailyLossLimit)
        {
            isTripped = true;
            tripReason = $"Maximum daily loss limit reached: Total PnL is ₹{totalPnLToday:F2} (Limit: -₹{dailyLossLimit:F2}).";

            // Auto lock account in database if not yet locked
            if (!account.IsTradingLockedForDay)
            {
                account.IsTradingLockedForDay = true;
                account.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        else if (account.IsTradingLockedForDay)
        {
            isTripped = true;
            tripReason = "Trading manually locked for the day.";
        }
        else if (tradesCountToday >= _riskOptions.MaxDailyTrades)
        {
            isTripped = true;
            tripReason = $"Maximum daily trades limit ({_riskOptions.MaxDailyTrades}) reached.";
        }
        else if (consecutiveLosses >= _riskOptions.MaxConsecutiveLosses)
        {
            isTripped = true;
            tripReason = $"Consecutive loss limit reached: {consecutiveLosses} consecutive losses recorded.";
        }

        bool canOpenPositions = !isTripped && activeCount < _riskOptions.MaxConcurrentPositions;

        return new DailyRiskStatusDto
        {
            Date = istTodayStart,
            AccountName = account.AccountName,
            InitialCapital = account.InitialCapital,
            AvailableBalance = account.AvailableBalance,
            UtilizedMargin = account.UtilizedMargin,
            RealizedPnLToday = realizedPnLToday,
            UnrealizedPnLToday = unrealizedPnL,
            TotalPnLToday = totalPnLToday,
            DailyLossLimit = dailyLossLimit,
            DailyLossRemaining = dailyLossRemaining,
            TradesCountToday = tradesCountToday,
            MaxDailyTradesLimit = _riskOptions.MaxDailyTrades,
            ConsecutiveLossesToday = consecutiveLosses,
            MaxConsecutiveLossesLimit = _riskOptions.MaxConsecutiveLosses,
            ActivePositionsCount = activeCount,
            MaxConcurrentPositionsLimit = _riskOptions.MaxConcurrentPositions,
            IsCircuitBreakerTripped = isTripped,
            CircuitBreakerReason = tripReason,
            CanOpenNewPositions = canOpenPositions
        };
    }

    public async Task<PaperAccount> GetOrCreateDefaultAccountAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
        return await GetOrCreateAccountInternalAsync(db, ct);
    }

    public async Task RecordTradeResultAsync(PaperTrade trade, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();

        var account = await GetOrCreateAccountInternalAsync(db, ct);

        // Add trade to database if not already tracked
        if (trade.Id == Guid.Empty) trade.Id = Guid.NewGuid();
        if (trade.ExitTimeUtc == default) trade.ExitTimeUtc = DateTime.UtcNow;

        var existingTrade = await db.PaperTrades.FirstOrDefaultAsync(t => t.Id == trade.Id, ct);
        if (existingTrade == null)
        {
            db.PaperTrades.Add(trade);
        }

        // Update Account balance and realized PnL
        account.RealizedPnL += trade.NetPnL;
        account.AvailableBalance += trade.NetPnL;
        account.UpdatedAtUtc = DateTime.UtcNow;

        // Check if daily loss breached
        var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
        var utcTodayStart = TimeZoneInfo.ConvertTimeToUtc(istNow.Date, IstZone);
        var tradesToday = await db.PaperTrades
            .Where(t => t.ExitTimeUtc >= utcTodayStart)
            .ToListAsync(ct);

        decimal todayRealized = tradesToday.Sum(t => t.NetPnL);
        if (todayRealized <= -account.DailyLossLimit)
        {
            account.IsTradingLockedForDay = true;
            _logger.LogWarning("Daily loss limit exceeded! Trading locked for the day. Today's Realized PnL: ₹{PnL}", todayRealized);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ResetDailyLimitsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();

        var account = await GetOrCreateAccountInternalAsync(db, ct);
        account.IsTradingLockedForDay = false;
        account.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Daily risk limits and circuit breaker reset successfully.");
    }

    public PositionSizingResult CalculatePositionSize(decimal entryPremium, decimal stopLossPremium, decimal availableBalance)
    {
        var accountRisk = Math.Round(_riskOptions.InitialCapital * (_riskOptions.RiskPerTradePercent / 100m), 2);
        return _calculator.CalculatePositionSize(
            entryPremium,
            stopLossPremium,
            availableBalance,
            accountRisk,
            PositionSizingCalculator.DefaultLotSize,
            _riskOptions.MaxConcurrentPositions);
    }

    private async Task<PaperAccount> GetOrCreateAccountInternalAsync(NiftyQuantDbContext db, CancellationToken ct)
    {
        var account = await db.PaperAccounts.FirstOrDefaultAsync(ct);
        if (account == null)
        {
            decimal initialCap = _riskOptions.InitialCapital > 0m ? _riskOptions.InitialCapital : 50000m;
            decimal dailyLoss = Math.Round(initialCap * (_riskOptions.MaxDailyLossPercent / 100m), 2);
            decimal tradeRisk = Math.Round(initialCap * (_riskOptions.RiskPerTradePercent / 100m), 2);

            account = new PaperAccount
            {
                AccountName = "Default Paper Account",
                InitialCapital = initialCap,
                AvailableBalance = initialCap,
                UtilizedMargin = 0m,
                RealizedPnL = 0m,
                UnrealizedPnL = 0m,
                DailyLossLimit = dailyLoss,
                MaxRiskPerTrade = tradeRisk,
                IsTradingLockedForDay = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            db.PaperAccounts.Add(account);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Initialized Default Paper Account: Capital=₹{Capital}, DailyLossLimit=₹{LossLimit}, TradeRisk=₹{TradeRisk}",
                initialCap, dailyLoss, tradeRisk);
        }
        else
        {
            decimal initialCap = _riskOptions.InitialCapital > 0m ? _riskOptions.InitialCapital : 50000m;
            decimal dailyLoss = Math.Round(initialCap * (_riskOptions.MaxDailyLossPercent / 100m), 2);
            decimal tradeRisk = Math.Round(initialCap * (_riskOptions.RiskPerTradePercent / 100m), 2);

            if (account.DailyLossLimit != dailyLoss || account.MaxRiskPerTrade != tradeRisk)
            {
                account.DailyLossLimit = dailyLoss;
                account.MaxRiskPerTrade = tradeRisk;
                await db.SaveChangesAsync(ct);
            }
        }

        return account;
    }

    private static TimeZoneInfo GetIstTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
    }
}
