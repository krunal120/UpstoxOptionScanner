using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Services.Strategy;

public class TrendFollowingStrategyEvaluator : IStrategyEvaluator
{
    public StrategyFamily Family => StrategyFamily.TrendFollowing;

    public StrategyEvaluationResult Evaluate(StrategyAnalysisContext context, MarketRegimeResult regime)
    {
        var result = new StrategyEvaluationResult
        {
            StrategyFamily = Family,
            HasSignal = false,
            Direction = SignalDirection.Wait
        };

        var mtf = context.MultiTimeframeState;
        var snap15m = mtf.Timeframe15m;
        var snap5m = mtf.Timeframe5m;
        var snap1m = mtf.Timeframe1m;
        var options = context.OptionsReport;
        var spot = context.CurrentPrice;

        if (snap15m == null || snap5m == null)
        {
            result.Reasons.Add(new SignalReasonDto { Category = "Data", Description = "Insufficient multi-timeframe candle data for Trend Following." });
            return result;
        }

        // Bullish Evaluation
        if (regime.TrendAllowedBullish)
        {
            decimal bullScore = 0m;
            var bullReasons = new List<SignalReasonDto>();

            // 1. Primary Trend Alignment (15m & 5m)
            if (snap15m.Ema9 > snap15m.Ema21 && spot > snap15m.Ema21)
            {
                bullScore += 25m;
                bullReasons.Add(new SignalReasonDto { Category = "Trend", ScoreContribution = 25m, Description = "15m Primary Trend is strongly Bullish (EMA 9 > 21 and price above EMA 21)." });
            }

            // 2. VWAP Relationship
            if (snap5m.Vwap.HasValue && spot >= snap5m.Vwap.Value)
            {
                bullScore += 15m;
                bullReasons.Add(new SignalReasonDto { Category = "VolumeBreadth", ScoreContribution = 15m, Description = $"Price {spot:F1} is above intraday VWAP {snap5m.Vwap.Value:F1}." });
            }

            // 3. Supertrend Agreement
            if (snap15m.SupertrendIsBullish == true && snap5m.SupertrendIsBullish == true)
            {
                bullScore += 20m;
                bullReasons.Add(new SignalReasonDto { Category = "Trend", ScoreContribution = 20m, Description = "Multi-timeframe Supertrend (15m & 5m) is firmly Bullish." });
            }

            // 4. Momentum (RSI & MACD)
            if (snap5m.Rsi14.HasValue && snap5m.Rsi14.Value >= 52m && snap5m.Rsi14.Value <= 70m)
            {
                bullScore += 15m;
                bullReasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 15m, Description = $"5m RSI is in optimal bullish momentum zone: {snap5m.Rsi14.Value:F1}." });
            }

            // 5. Options Flow Confirmation
            if (options.OptionsSentimentScore >= 15 || options.PcrAnalysis.PcrOi >= 1.05m)
            {
                bullScore += 25m;
                bullReasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 25m, Description = $"Options flow confirms bullish sentiment: Score {options.OptionsSentimentScore}/100, PCR {options.PcrAnalysis.PcrOi:F2}." });
            }

            result.BullishScore = bullScore;
            if (bullScore >= 65m)
            {
                result.HasSignal = true;
                result.Direction = bullScore >= 80m ? SignalDirection.StrongBuyCe : SignalDirection.BuyCe;
                result.Confidence = bullScore;
                result.Reasons = bullReasons;

                // Stop Loss: VWAP or recent 5m EMA 21 (minimum 20 pts)
                decimal slBase = Math.Min(snap5m.Ema21 ?? spot - 25m, snap5m.Vwap ?? spot - 25m);
                decimal riskDistance = Math.Max(20m, spot - slBase);
                result.StopLossUnderlyingDistance = Math.Round(riskDistance, 1);
                result.Target1UnderlyingDistance = Math.Round(riskDistance * 2m, 1);
                result.Target2UnderlyingDistance = Math.Round(riskDistance * 3m, 1);
                return result;
            }
        }

        // Bearish Evaluation
        if (regime.TrendAllowedBearish)
        {
            decimal bearScore = 0m;
            var bearReasons = new List<SignalReasonDto>();

            // 1. Primary Trend Alignment (15m & 5m)
            if (snap15m.Ema9 < snap15m.Ema21 && spot < snap15m.Ema21)
            {
                bearScore += 25m;
                bearReasons.Add(new SignalReasonDto { Category = "Trend", ScoreContribution = 25m, Description = "15m Primary Trend is strongly Bearish (EMA 9 < 21 and price below EMA 21)." });
            }

            // 2. VWAP Relationship
            if (snap5m.Vwap.HasValue && spot <= snap5m.Vwap.Value)
            {
                bearScore += 15m;
                bearReasons.Add(new SignalReasonDto { Category = "VolumeBreadth", ScoreContribution = 15m, Description = $"Price {spot:F1} is below intraday VWAP {snap5m.Vwap.Value:F1}." });
            }

            // 3. Supertrend Agreement
            if (snap15m.SupertrendIsBullish == false && snap5m.SupertrendIsBullish == false)
            {
                bearScore += 20m;
                bearReasons.Add(new SignalReasonDto { Category = "Trend", ScoreContribution = 20m, Description = "Multi-timeframe Supertrend (15m & 5m) is firmly Bearish." });
            }

            // 4. Momentum (RSI & MACD)
            if (snap5m.Rsi14.HasValue && snap5m.Rsi14.Value <= 48m && snap5m.Rsi14.Value >= 30m)
            {
                bearScore += 15m;
                bearReasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 15m, Description = $"5m RSI is in optimal bearish breakdown zone: {snap5m.Rsi14.Value:F1}." });
            }

            // 5. Options Flow Confirmation
            if (options.OptionsSentimentScore <= -15 || options.PcrAnalysis.PcrOi <= 0.95m)
            {
                bearScore += 25m;
                bearReasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 25m, Description = $"Options flow confirms bearish sentiment: Score {options.OptionsSentimentScore}/100, PCR {options.PcrAnalysis.PcrOi:F2}." });
            }

            result.BearishScore = bearScore;
            if (bearScore >= 65m)
            {
                result.HasSignal = true;
                result.Direction = bearScore >= 80m ? SignalDirection.StrongBuyPe : SignalDirection.BuyPe;
                result.Confidence = bearScore;
                result.Reasons = bearReasons;

                decimal slBase = Math.Max(snap5m.Ema21 ?? spot + 25m, snap5m.Vwap ?? spot + 25m);
                decimal riskDistance = Math.Max(20m, slBase - spot);
                result.StopLossUnderlyingDistance = Math.Round(riskDistance, 1);
                result.Target1UnderlyingDistance = Math.Round(riskDistance * 2m, 1);
                result.Target2UnderlyingDistance = Math.Round(riskDistance * 3m, 1);
                return result;
            }
        }

        return result;
    }
}
