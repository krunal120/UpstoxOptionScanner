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

            // 2. VWAP Relationship & No-Chase Guard
            if (snap5m.Vwap.HasValue)
            {
                var vwap = snap5m.Vwap.Value;
                if (spot >= vwap)
                {
                    var distFromVwap = spot - vwap;
                    if (distFromVwap > 55m)
                    {
                        // Over-extended! Deduct 20 points
                        bullScore -= 20m;
                        bullReasons.Add(new SignalReasonDto 
                        { 
                            Category = "VolumeBreadth", 
                            ScoreContribution = -20m, 
                            Description = $"No-Chase Warning: Spot {spot:F1} is over-extended {distFromVwap:F1} pts above VWAP {vwap:F1}." 
                        });
                    }
                    else if (distFromVwap <= 35m || (snap5m.Ema21.HasValue && Math.Abs(spot - snap5m.Ema21.Value) <= 20m))
                    {
                        // Pullback / optimal continuation zone
                        bullScore += 20m;
                        bullReasons.Add(new SignalReasonDto 
                        { 
                            Category = "VolumeBreadth", 
                            ScoreContribution = 20m, 
                            Description = $"Optimal Entry: Spot {spot:F1} is near VWAP/EMA21 support ({distFromVwap:F1} pts from VWAP)." 
                        });
                    }
                    else
                    {
                        // Moderate extension (35-55 pts)
                        bullScore += 10m;
                        bullReasons.Add(new SignalReasonDto 
                        { 
                            Category = "VolumeBreadth", 
                            ScoreContribution = 10m, 
                            Description = $"Acceptable Entry: Spot {spot:F1} is {distFromVwap:F1} pts above VWAP." 
                        });
                    }
                }
                else
                {
                    bullReasons.Add(new SignalReasonDto 
                    { 
                        Category = "VolumeBreadth", 
                        ScoreContribution = 0m, 
                        Description = $"Price {spot:F1} is below intraday VWAP {vwap:F1} (Lack of bullish VWAP confirmation)." 
                    });
                }
            }

            // 3. Supertrend Agreement
            if (snap15m.SupertrendIsBullish == true && snap5m.SupertrendIsBullish == true)
            {
                bullScore += 20m;
                bullReasons.Add(new SignalReasonDto { Category = "Trend", ScoreContribution = 20m, Description = "Multi-timeframe Supertrend (15m & 5m) is firmly Bullish." });
            }

            // 4. Momentum (RSI Sweet Spot vs Overbought Exhaustion)
            if (snap5m.Rsi14.HasValue)
            {
                var rsi = snap5m.Rsi14.Value;
                if (rsi >= 50m && rsi <= 63m)
                {
                    bullScore += 15m;
                    bullReasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 15m, Description = $"5m RSI is in optimal bullish momentum zone: {rsi:F1}." });
                }
                else if (rsi > 63m && rsi <= 66m)
                {
                    bullScore += 5m;
                    bullReasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 5m, Description = $"5m RSI is mildly extended: {rsi:F1}." });
                }
                else if (rsi > 66m)
                {
                    bullScore -= 15m;
                    bullReasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = -15m, Description = $"Overbought Exhaustion: 5m RSI is stretched at {rsi:F1}." });
                }
            }

            // 5. Options Flow Confirmation (Strict PCR & Sentiment - No loose OR condition)
            if (options != null && options.PcrAnalysis != null)
            {
                var pcr = options.PcrAnalysis.PcrOi;
                if (pcr >= 1.05m)
                {
                    bullScore += 25m;
                    bullReasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 25m, Description = $"Institutional Put writing cushion: PCR {pcr:F2} >= 1.05." });
                }
                else if (pcr >= 0.98m && options.OptionsSentimentScore >= 15)
                {
                    bullScore += 10m;
                    bullReasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 10m, Description = $"Mild bullish options flow: PCR {pcr:F2}, Sentiment Score {options.OptionsSentimentScore}." });
                }
                else if (pcr < 0.95m)
                {
                    bullScore -= 20m;
                    bullReasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = -20m, Description = $"Heavy Call Writing Resistance: PCR {pcr:F2} < 0.95 acts as strong ceiling for CE." });
                }
            }

            result.BullishScore = bullScore;
            result.Reasons = bullReasons;
            if (bullScore >= 65m)
            {
                result.HasSignal = true;
                result.Direction = bullScore >= 80m ? SignalDirection.StrongBuyCe : SignalDirection.BuyCe;
                result.Confidence = bullScore;

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

            // 2. VWAP Relationship & No-Chase Guard
            if (snap5m.Vwap.HasValue)
            {
                var vwap = snap5m.Vwap.Value;
                if (spot <= vwap)
                {
                    var distFromVwap = vwap - spot;
                    if (distFromVwap > 55m)
                    {
                        // Over-dumped! Deduct 20 points
                        bearScore -= 20m;
                        bearReasons.Add(new SignalReasonDto 
                        { 
                            Category = "VolumeBreadth", 
                            ScoreContribution = -20m, 
                            Description = $"No-Chase Warning: Spot {spot:F1} is over-extended {distFromVwap:F1} pts below VWAP {vwap:F1}." 
                        });
                    }
                    else if (distFromVwap <= 35m || (snap5m.Ema21.HasValue && Math.Abs(spot - snap5m.Ema21.Value) <= 20m))
                    {
                        // Pullback / optimal continuation zone
                        bearScore += 20m;
                        bearReasons.Add(new SignalReasonDto 
                        { 
                            Category = "VolumeBreadth", 
                            ScoreContribution = 20m, 
                            Description = $"Optimal Entry: Spot {spot:F1} is near VWAP/EMA21 resistance ({distFromVwap:F1} pts from VWAP)." 
                        });
                    }
                    else
                    {
                        // Moderate extension (35-55 pts)
                        bearScore += 10m;
                        bearReasons.Add(new SignalReasonDto 
                        { 
                            Category = "VolumeBreadth", 
                            ScoreContribution = 10m, 
                            Description = $"Acceptable Entry: Spot {spot:F1} is {distFromVwap:F1} pts below VWAP." 
                        });
                    }
                }
                else
                {
                    bearReasons.Add(new SignalReasonDto 
                    { 
                        Category = "VolumeBreadth", 
                        ScoreContribution = 0m, 
                        Description = $"Price {spot:F1} is above intraday VWAP {vwap:F1} (Lack of bearish VWAP confirmation)." 
                    });
                }
            }

            // 3. Supertrend Agreement
            if (snap15m.SupertrendIsBullish == false && snap5m.SupertrendIsBullish == false)
            {
                bearScore += 20m;
                bearReasons.Add(new SignalReasonDto { Category = "Trend", ScoreContribution = 20m, Description = "Multi-timeframe Supertrend (15m & 5m) is firmly Bearish." });
            }

            // 4. Momentum (RSI Sweet Spot vs Oversold Exhaustion)
            if (snap5m.Rsi14.HasValue)
            {
                var rsi = snap5m.Rsi14.Value;
                if (rsi >= 37m && rsi <= 50m)
                {
                    bearScore += 15m;
                    bearReasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 15m, Description = $"5m RSI is in optimal bearish breakdown zone: {rsi:F1}." });
                }
                else if (rsi >= 34m && rsi < 37m)
                {
                    bearScore += 5m;
                    bearReasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = 5m, Description = $"5m RSI is mildly oversold: {rsi:F1}." });
                }
                else if (rsi < 34m)
                {
                    bearScore -= 15m;
                    bearReasons.Add(new SignalReasonDto { Category = "Momentum", ScoreContribution = -15m, Description = $"Oversold Exhaustion: 5m RSI is dumped at {rsi:F1}." });
                }
            }

            // 5. Options Flow Confirmation (Strict PCR & Sentiment - No loose OR condition)
            if (options != null && options.PcrAnalysis != null)
            {
                var pcr = options.PcrAnalysis.PcrOi;
                if (pcr <= 0.90m)
                {
                    bearScore += 25m;
                    bearReasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 25m, Description = $"Institutional Call writing ceiling: PCR {pcr:F2} <= 0.90." });
                }
                else if (pcr <= 1.02m && options.OptionsSentimentScore <= -15)
                {
                    bearScore += 10m;
                    bearReasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = 10m, Description = $"Mild bearish options flow: PCR {pcr:F2}, Sentiment Score {options.OptionsSentimentScore}." });
                }
                else if (pcr > 1.05m)
                {
                    bearScore -= 20m;
                    bearReasons.Add(new SignalReasonDto { Category = "OptionChain", ScoreContribution = -20m, Description = $"Heavy Put Writing Support: PCR {pcr:F2} > 1.05 acts as strong floor against PE." });
                }
            }

            result.BearishScore = bearScore;
            result.Reasons = bearReasons;
            if (bearScore >= 65m)
            {
                result.HasSignal = true;
                result.Direction = bearScore >= 80m ? SignalDirection.StrongBuyPe : SignalDirection.BuyPe;
                result.Confidence = bearScore;

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
