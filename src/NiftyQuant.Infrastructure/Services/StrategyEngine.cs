using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;

namespace NiftyQuant.Infrastructure.Services;

public class StrategyEngine : IStrategyEngine
{
    private static readonly TimeZoneInfo IstZone = GetIstTimeZone();

    private readonly IMarketRegimeClassifier _regimeClassifier;
    private readonly IEnumerable<IStrategyEvaluator> _evaluators;
    private readonly IOptionsIntelligenceService _optionsIntelligence;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RiskOptions _riskOptions;
    private readonly ILogger<StrategyEngine> _logger;

    public event Func<StrategySignal, Task>? OnSignalGenerated;

    public StrategyEngine(
        IMarketRegimeClassifier regimeClassifier,
        IEnumerable<IStrategyEvaluator> evaluators,
        IOptionsIntelligenceService optionsIntelligence,
        IServiceScopeFactory scopeFactory,
        IOptions<RiskOptions> riskOptions,
        ILogger<StrategyEngine> logger)
    {
        _regimeClassifier = regimeClassifier;
        _evaluators = evaluators;
        _optionsIntelligence = optionsIntelligence;
        _scopeFactory = scopeFactory;
        _riskOptions = riskOptions.Value;
        _logger = logger;
    }

    public async Task<StrategySignal> EvaluateStrategiesAsync(
        StrategyAnalysisContext context,
        CancellationToken ct = default)
    {
        var regime = _regimeClassifier.ClassifyRegime(context);
        var spot = context.CurrentPrice;

        // Evaluate all strategy families
        StrategyEvaluationResult? bestEvaluation = null;
        decimal highestScore = -1m;

        foreach (var evaluator in _evaluators)
        {
            try
            {
                var eval = evaluator.Evaluate(context, regime);
                decimal score = Math.Max(eval.BullishScore, eval.BearishScore);
                if (eval.HasSignal && score > highestScore)
                {
                    highestScore = score;
                    bestEvaluation = eval;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing strategy evaluator {Family}", evaluator.Family);
            }
        }

        // Composite scoring
        decimal compositeBullScore = bestEvaluation?.BullishScore ?? 0m;
        decimal compositeBearScore = bestEvaluation?.BearishScore ?? 0m;
        SignalDirection direction = bestEvaluation?.Direction ?? SignalDirection.Wait;
        StrategyFamily family = bestEvaluation?.StrategyFamily ?? StrategyFamily.None;

        // Apply Time-of-Day Veto (IST market session: 09:15 to 15:30)
        var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(context.EvaluationTimeUtc, DateTimeKind.Utc), IstZone);
        var sessionStartTrading = new TimeSpan(9, 20, 0); // No trades in opening 5 minutes
        var sessionEndTrading = new TimeSpan(15, 10, 0);   // No new trades in final 20 minutes

        var reasons = new List<SignalReason>();
        if (bestEvaluation?.Reasons != null)
        {
            reasons.AddRange(bestEvaluation.Reasons.Select(r => new SignalReason
            {
                Category = r.Category,
                ScoreContribution = r.ScoreContribution,
                Description = r.Description
            }));
        }

        var technicalDirection = direction;

        if (istNow.TimeOfDay < sessionStartTrading || istNow.TimeOfDay > sessionEndTrading)
        {
            if (direction != SignalDirection.Wait && direction != SignalDirection.NoTrade)
            {
                direction = SignalDirection.NoTrade;
                reasons.Add(new SignalReason
                {
                    Category = "RiskVeto",
                    ScoreContribution = 0m,
                    Description = $"Time-of-day veto: Current IST time {istNow:HH:mm} is outside the active trading window (09:20 - 15:10)."
                });
            }
        }

        // Apply High Volatility Veto
        if (regime.Regime == MarketRegimeType.HighVolatility)
        {
            direction = SignalDirection.NoTrade;
            reasons.Add(new SignalReason
            {
                Category = "RiskVeto",
                ScoreContribution = 0m,
                Description = "High Volatility Veto: India VIX or market ATR is too elevated for non-hedged options buying."
            });
        }

        // Contract selection & premium calculation based on technical direction (preserves optimal contract even when vetoed)
        SelectedOptionContractDto? selectedContract = null;
        if (technicalDirection == SignalDirection.BuyCe || technicalDirection == SignalDirection.StrongBuyCe ||
            (direction == SignalDirection.NoTrade && compositeBullScore >= compositeBearScore && compositeBullScore >= 50m))
        {
            selectedContract = context.OptionsReport.RecommendedCe ?? context.OptionsReport.RecommendedPe;
        }
        else if (technicalDirection == SignalDirection.BuyPe || technicalDirection == SignalDirection.StrongBuyPe ||
            (direction == SignalDirection.NoTrade && compositeBearScore > compositeBullScore && compositeBearScore >= 50m))
        {
            selectedContract = context.OptionsReport.RecommendedPe ?? context.OptionsReport.RecommendedCe;
        }
        else
        {
            // Fallback to whichever recommended contract has the higher liquidity score
            selectedContract = (context.OptionsReport.RecommendedCe?.LiquidityScore ?? 0) >= (context.OptionsReport.RecommendedPe?.LiquidityScore ?? 0)
                ? (context.OptionsReport.RecommendedCe ?? context.OptionsReport.RecommendedPe)
                : (context.OptionsReport.RecommendedPe ?? context.OptionsReport.RecommendedCe);
        }

        // Premium level calculations
        decimal entryPremium = selectedContract?.Ltp ?? 0m;
        decimal absDelta = Math.Abs(selectedContract?.Delta ?? 0.50m);
        decimal stopLossDist = bestEvaluation?.StopLossUnderlyingDistance ?? 25m;
        decimal target1Dist = bestEvaluation?.Target1UnderlyingDistance ?? (stopLossDist * 2m);
        decimal target2Dist = bestEvaluation?.Target2UnderlyingDistance ?? (stopLossDist * 3m);

        decimal slPremium = 0m;
        decimal t1Premium = 0m;
        decimal t2Premium = 0m;
        decimal rRatio = 0m;
        decimal riskAmount = 0m;
        int lotSize = 65; // NIFTY lot size

        if (selectedContract != null && entryPremium > 0m)
        {
            decimal premiumRisk = Math.Round(stopLossDist * absDelta, 2);
            slPremium = Math.Max(1.0m, Math.Round(entryPremium - premiumRisk, 2));

            decimal premiumReward1 = Math.Round(target1Dist * absDelta, 2);
            t1Premium = Math.Round(entryPremium + premiumReward1, 2);

            decimal premiumReward2 = Math.Round(target2Dist * absDelta, 2);
            t2Premium = Math.Round(entryPremium + premiumReward2, 2);

            decimal perLotRisk = (entryPremium - slPremium) * lotSize;
            riskAmount = Math.Round(perLotRisk, 2);

            rRatio = (entryPremium - slPremium) > 0m
                ? Math.Round((t1Premium - entryPremium) / (entryPremium - slPremium), 2)
                : 2.0m;
        }

        var signal = new StrategySignal
        {
            Id = Guid.NewGuid(),
            SignalTimeUtc = context.EvaluationTimeUtc,
            UnderlyingKey = context.UnderlyingKey,
            UnderlyingPrice = spot,
            Direction = direction,
            StrategyFamily = family,
            MarketRegime = regime.Regime,
            BullishScore = compositeBullScore,
            BearishScore = compositeBearScore,
            Confidence = Math.Max(compositeBullScore, compositeBearScore),
            SelectedInstrumentKey = selectedContract?.InstrumentKey ?? string.Empty,
            SelectedStrike = selectedContract?.StrikePrice ?? 0m,
            SelectedOptionType = selectedContract?.OptionType ?? (direction.ToString().Contains("Ce") ? OptionType.CE : OptionType.PE),
            SelectedExpiry = selectedContract?.ExpiryDate ?? DateTime.UtcNow.Date,
            EntryPremium = entryPremium,
            StopLossPremium = slPremium,
            Target1Premium = t1Premium,
            Target2Premium = t2Premium,
            RecommendedQuantity = lotSize,
            LotSize = lotSize,
            RiskAmount = riskAmount,
            RewardToRiskRatio = rRatio,
            ConfigurationVersion = "1.0",
            IsExecuted = false,
            Reasons = reasons
        };

        // Persist to database if scope factory available
        if (_scopeFactory != null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService(typeof(NiftyQuantDbContext)) as NiftyQuantDbContext;
                if (db != null)
                {
                    db.StrategySignals.Add(signal);
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist StrategySignal to database.");
            }
        }

        _logger.LogInformation("Strategy Evaluation: Direction={Direction}, Family={Family}, Regime={Regime}, Bull={Bull}, Bear={Bear}, Contract={Contract}",
            direction, family, regime.Regime, compositeBullScore, compositeBearScore, signal.SelectedInstrumentKey);

        // Notify subscribers (SignalR hub, Paper Trading Engine)
        if (OnSignalGenerated != null)
        {
            try
            {
                await OnSignalGenerated.Invoke(signal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching OnSignalGenerated event.");
            }
        }

        return signal;
    }

    public async Task<StrategySignal?> GetLatestSignalAsync(
        string underlyingKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default)
    {
        if (_scopeFactory == null) return null;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService(typeof(NiftyQuantDbContext)) as NiftyQuantDbContext;
        if (db == null) return null;

        return await db.StrategySignals
            .Include(s => s.Reasons)
            .Where(s => s.UnderlyingKey == underlyingKey)
            .OrderByDescending(s => s.SignalTimeUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<StrategySignal>> GetSignalHistoryAsync(
        string underlyingKey = "NSE_INDEX|Nifty 50",
        int limit = 50,
        CancellationToken ct = default)
    {
        if (_scopeFactory == null) return new List<StrategySignal>();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService(typeof(NiftyQuantDbContext)) as NiftyQuantDbContext;
        if (db == null) return new List<StrategySignal>();

        return await db.StrategySignals
            .Include(s => s.Reasons)
            .Where(s => s.UnderlyingKey == underlyingKey)
            .OrderByDescending(s => s.SignalTimeUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    private static TimeZoneInfo GetIstTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
    }
}
