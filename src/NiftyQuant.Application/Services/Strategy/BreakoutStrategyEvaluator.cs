using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Services.Strategy;

public class BreakoutStrategyEvaluator : IStrategyEvaluator
{
    public StrategyFamily Family => StrategyFamily.Breakout;

    public StrategyEvaluationResult Evaluate(StrategyAnalysisContext context, MarketRegimeResult regime)
    {
        var result = new StrategyEvaluationResult
        {
            StrategyFamily = Family,
            HasSignal = false,
            Direction = SignalDirection.Wait
        };

        var spot = context.CurrentPrice;
        var mtf = context.MultiTimeframeState;
        var snap5m = mtf.Timeframe5m;
        var snap15m = mtf.Timeframe15m;
        var options = context.OptionsReport;
        var levels = options.KeyLevels;
        var cpr = mtf.DailyCpr;

        // Breakouts are strictly invalid in High Volatility, Sideways, or Transitional regimes
        if (regime.Regime == MarketRegimeType.HighVolatility ||
            regime.Regime == MarketRegimeType.Sideways ||
            regime.Regime == MarketRegimeType.Transitional)
        {
            return result;
        }

        decimal rvol = snap5m?.RelativeVolume ?? 1.0m;
        decimal adx = snap15m?.Adx14 ?? snap5m?.Adx14 ?? 20m;
        decimal candle5mClose = (snap5m != null && snap5m.ClosePrice > 0m) ? snap5m.ClosePrice : spot;

        // Breakouts require minimum institutional participation (RVol >= 1.1x)
        if (rvol < 1.1m) return result;

        // 1. Bullish Breakout Evaluation
        // Must be in StrongBullish regime and confirmed by 5m candle close
        if (regime.TrendAllowedBullish)
        {
            bool breaksPdh = context.PreviousDayHigh > 0m && candle5mClose > context.PreviousDayHigh && (spot - context.PreviousDayHigh) <= 50m;
            bool breaksCallWall = levels.MajorCallResistance > 0m && candle5mClose >= levels.MajorCallResistance && (spot - levels.MajorCallResistance) <= 50m;
            bool breaksCprTop = cpr != null && cpr.IsNarrowCpr && candle5mClose > cpr.CprTop && (spot - cpr.CprTop) <= 40m;

            if (breaksPdh || breaksCallWall || breaksCprTop)
            {
                decimal score = 0m;
                var reasons = new List<SignalReasonDto>();

                if (breaksCallWall)
                {
                    score += 35m;
                    reasons.Add(new SignalReasonDto { Category = "PriceAction", ScoreContribution = 35m, Description = $"Breakout above Major Call Resistance {levels.MajorCallResistance:F0} triggering Call Short-Covering squeeze." });
                }
                else if (breaksPdh)
                {
                    score += 30m;
                    reasons.Add(new SignalReasonDto { Category = "PriceAction", ScoreContribution = 30m, Description = $"Breakout above Previous Day High {context.PreviousDayHigh:F0}." });
                }
                else if (breaksCprTop)
                {
                    score += 25m;
                    reasons.Add(new SignalReasonDto { Category = "PriceAction", ScoreContribution = 25m, Description = $"Narrow CPR Breakout above Top Central Pivot {cpr!.CprTop:F0}." });
                }

                // Volume surge confirmation
                if (rvol >= 1.3m)
                {
                    score += 25m;
                    reasons.Add(new SignalReasonDto { Category = "VolumeBreadth", ScoreContribution = 25m, Description = $"Institutional volume surge: RVol {rvol:F1}x above 20-period average." });
                }
                else if (rvol >= 1.0m)
                {
                    score += 15m;
                }

                // ADX trend strength
                if (adx >= 22m)
                {
                    score += 20m;
                    reasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 20m, Description = $"Strong trend expansion: ADX at {adx:F1}." });
                }

                // Options confirmation
                if (options.OptionsSentimentScore >= 10 || options.PcrAnalysis.PcrOi >= 1.0m)
                {
                    score += 20m;
                    reasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 20m, Description = $"Options flow confirms breakout: PCR {options.PcrAnalysis.PcrOi:F2}." });
                }

                result.BullishScore = score;
                if (score >= 65m)
                {
                    result.HasSignal = true;
                    result.Direction = score >= 80m ? SignalDirection.StrongBuyCe : SignalDirection.BuyCe;
                    result.Confidence = score;
                    result.Reasons = reasons;

                    decimal breakLevel = breaksCallWall ? levels.MajorCallResistance : (breaksPdh ? context.PreviousDayHigh : cpr!.CprTop);
                    decimal riskDistance = Math.Max(20m, (spot - breakLevel) + 15m);
                    result.StopLossUnderlyingDistance = Math.Round(riskDistance, 1);
                    result.Target1UnderlyingDistance = Math.Round(riskDistance * 2m, 1);
                    result.Target2UnderlyingDistance = Math.Round(riskDistance * 3m, 1);
                    return result;
                }
            }
        }

        // 2. Bearish Breakdown Evaluation
        // Must be in StrongBearish regime and confirmed by 5m candle close
        if (regime.TrendAllowedBearish)
        {
            bool breaksPdl = context.PreviousDayLow > 0m && candle5mClose < context.PreviousDayLow && (context.PreviousDayLow - spot) <= 50m;
            bool breaksPutFloor = levels.MajorPutSupport > 0m && candle5mClose <= levels.MajorPutSupport && (levels.MajorPutSupport - spot) <= 50m;
            bool breaksCprBottom = cpr != null && cpr.IsNarrowCpr && candle5mClose < cpr.CprBottom && (cpr.CprBottom - spot) <= 40m;

            if (breaksPdl || breaksPutFloor || breaksCprBottom)
            {
                decimal score = 0m;
                var reasons = new List<SignalReasonDto>();

                if (breaksPutFloor)
                {
                    score += 35m;
                    reasons.Add(new SignalReasonDto { Category = "PriceAction", ScoreContribution = 35m, Description = $"Breakdown below Major Put Support {levels.MajorPutSupport:F0} triggering Put Long-Unwinding cascade." });
                }
                else if (breaksPdl)
                {
                    score += 30m;
                    reasons.Add(new SignalReasonDto { Category = "PriceAction", ScoreContribution = 30m, Description = $"Breakdown below Previous Day Low {context.PreviousDayLow:F0}." });
                }
                else if (breaksCprBottom)
                {
                    score += 25m;
                    reasons.Add(new SignalReasonDto { Category = "PriceAction", ScoreContribution = 25m, Description = $"Narrow CPR Breakdown below Bottom Central Pivot {cpr!.CprBottom:F0}." });
                }

                if (rvol >= 1.3m)
                {
                    score += 25m;
                    reasons.Add(new SignalReasonDto { Category = "VolumeBreadth", ScoreContribution = 25m, Description = $"Institutional volume surge: RVol {rvol:F1}x above 20-period average." });
                }
                else if (rvol >= 1.0m)
                {
                    score += 15m;
                }

                if (adx >= 22m)
                {
                    score += 20m;
                    reasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 20m, Description = $"Strong breakdown expansion: ADX at {adx:F1}." });
                }

                if (options.OptionsSentimentScore <= -10 || options.PcrAnalysis.PcrOi <= 1.0m)
                {
                    score += 20m;
                    reasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 20m, Description = $"Options flow confirms breakdown: PCR {options.PcrAnalysis.PcrOi:F2}." });
                }

                result.BearishScore = score;
                if (score >= 65m)
                {
                    result.HasSignal = true;
                    result.Direction = score >= 80m ? SignalDirection.StrongBuyPe : SignalDirection.BuyPe;
                    result.Confidence = score;
                    result.Reasons = reasons;

                    decimal breakLevel = breaksPutFloor ? levels.MajorPutSupport : (breaksPdl ? context.PreviousDayLow : cpr!.CprBottom);
                    decimal riskDistance = Math.Max(20m, (breakLevel - spot) + 15m);
                    result.StopLossUnderlyingDistance = Math.Round(riskDistance, 1);
                    result.Target1UnderlyingDistance = Math.Round(riskDistance * 2m, 1);
                    result.Target2UnderlyingDistance = Math.Round(riskDistance * 3m, 1);
                    return result;
                }
            }
        }

        return result;
    }
}
