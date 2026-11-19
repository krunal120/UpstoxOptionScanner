using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Services.Strategy;

public class ReversalStrategyEvaluator : IStrategyEvaluator
{
    public StrategyFamily Family => StrategyFamily.Reversal;

    public StrategyEvaluationResult Evaluate(StrategyAnalysisContext context, MarketRegimeResult regime)
    {
        var result = new StrategyEvaluationResult
        {
            StrategyFamily = Family,
            HasSignal = false,
            Direction = SignalDirection.Wait
        };

        // Strict VETO: Reversals must never fade a strong trend or high volatility
        if (!regime.ReversalAllowed)
        {
            return result;
        }

        var spot = context.CurrentPrice;
        var mtf = context.MultiTimeframeState;
        var snap5m = mtf.Timeframe5m;
        var options = context.OptionsReport;
        var levels = options.KeyLevels;
        var cpr = mtf.DailyCpr;

        if (snap5m == null) return result;

        // 1. Bullish Mean Reversion (Buy CE at extreme support)
        bool atMajorPutSupport = Math.Abs(spot - levels.MajorPutSupport) <= 25m;
        bool atLowerBollinger = snap5m.BollingerLower.HasValue && spot <= snap5m.BollingerLower.Value + 15m;
        bool atS1OrS2 = cpr != null && (Math.Abs(spot - cpr.S1) <= 20m || Math.Abs(spot - cpr.S2) <= 20m);
        bool isOversoldRsi = snap5m.Rsi14.HasValue && snap5m.Rsi14.Value <= 35m;
        bool isOversoldPcr = options.PcrAnalysis.PcrOi <= 0.75m;

        if ((atMajorPutSupport || atLowerBollinger || atS1OrS2) && (isOversoldRsi || isOversoldPcr))
        {
            decimal score = 0m;
            var reasons = new List<SignalReasonDto>();

            score += 30m;
            reasons.Add(new SignalReasonDto { Category = "PriceAction", ScoreContribution = 30m, Description = $"Price is at major support floor (Put Support {levels.MajorPutSupport:F0} / S1 Pivot)." });

            if (isOversoldRsi)
            {
                score += 25m;
                reasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 25m, Description = $"5m RSI oversold at {snap5m.Rsi14!.Value:F1} signaling seller exhaustion." });
            }

            if (isOversoldPcr)
            {
                score += 25m;
                reasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 25m, Description = $"Extreme oversold Put-Call Ratio {options.PcrAnalysis.PcrOi:F2} ripe for mean reversion." });
            }

            result.BullishScore = score;
            if (score >= 65m)
            {
                result.HasSignal = true;
                result.Direction = SignalDirection.BuyCe;
                result.Confidence = score;
                result.Reasons = reasons;

                // Stop loss below the support extreme
                decimal supportBase = Math.Min(levels.MajorPutSupport, snap5m.BollingerLower ?? spot - 25m);
                decimal riskDistance = Math.Max(20m, (spot - supportBase) + 15m);
                result.StopLossUnderlyingDistance = Math.Round(riskDistance, 1);
                result.Target1UnderlyingDistance = Math.Round(riskDistance * 2m, 1);
                result.Target2UnderlyingDistance = Math.Round(riskDistance * 3m, 1);
                return result;
            }
        }

        // 2. Bearish Mean Reversion (Buy PE at extreme resistance)
        bool atMajorCallResistance = Math.Abs(spot - levels.MajorCallResistance) <= 25m;
        bool atUpperBollinger = snap5m.BollingerUpper.HasValue && spot >= snap5m.BollingerUpper.Value - 15m;
        bool atR1OrR2 = cpr != null && (Math.Abs(spot - cpr.R1) <= 20m || Math.Abs(spot - cpr.R2) <= 20m);
        bool isOverboughtRsi = snap5m.Rsi14.HasValue && snap5m.Rsi14.Value >= 65m;
        bool isOverboughtPcr = options.PcrAnalysis.PcrOi >= 1.35m;

        if ((atMajorCallResistance || atUpperBollinger || atR1OrR2) && (isOverboughtRsi || isOverboughtPcr))
        {
            decimal score = 0m;
            var reasons = new List<SignalReasonDto>();

            score += 30m;
            reasons.Add(new SignalReasonDto { Category = "PriceAction", ScoreContribution = 30m, Description = $"Price is at major resistance ceiling (Call Resistance {levels.MajorCallResistance:F0} / R1 Pivot)." });

            if (isOverboughtRsi)
            {
                score += 25m;
                reasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 25m, Description = $"5m RSI overbought at {snap5m.Rsi14!.Value:F1} signaling buyer exhaustion." });
            }

            if (isOverboughtPcr)
            {
                score += 25m;
                reasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 25m, Description = $"Extreme overbought Put-Call Ratio {options.PcrAnalysis.PcrOi:F2} ripe for mean reversion." });
            }

            result.BearishScore = score;
            if (score >= 65m)
            {
                result.HasSignal = true;
                result.Direction = SignalDirection.BuyPe;
                result.Confidence = score;
                result.Reasons = reasons;

                decimal resistBase = Math.Max(levels.MajorCallResistance, snap5m.BollingerUpper ?? spot + 25m);
                decimal riskDistance = Math.Max(20m, (resistBase - spot) + 15m);
                result.StopLossUnderlyingDistance = Math.Round(riskDistance, 1);
                result.Target1UnderlyingDistance = Math.Round(riskDistance * 2m, 1);
                result.Target2UnderlyingDistance = Math.Round(riskDistance * 3m, 1);
                return result;
            }
        }

        return result;
    }
}
