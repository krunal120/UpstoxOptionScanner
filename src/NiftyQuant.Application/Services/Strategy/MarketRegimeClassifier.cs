using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Services.Strategy;

public class MarketRegimeClassifier : IMarketRegimeClassifier
{
    public MarketRegimeResult ClassifyRegime(StrategyAnalysisContext context)
    {
        var mtf = context.MultiTimeframeState;
        var snap15m = mtf.Timeframe15m;
        var snap5m = mtf.Timeframe5m;
        var vix = context.IndiaVix ?? 15.0m;
        var adx15m = snap15m?.Adx14 ?? snap5m?.Adx14 ?? 20m;
        var spot = context.CurrentPrice;

        // 1. High Volatility Regime
        if (vix >= 22.0m)
        {
            return new MarketRegimeResult
            {
                Regime = MarketRegimeType.HighVolatility,
                Confidence = 85m,
                Description = $"High Volatility Regime: India VIX elevated at {vix:F1}. Elevated swings and option premium expansion.",
                AdxValue = adx15m,
                IndiaVix = vix,
                TrendAllowedBullish = false,
                TrendAllowedBearish = false,
                ReversalAllowed = false
            };
        }

        // 1b. CPR Equilibrium Trap: If price is oscillating inside Central Pivot Range (BC to TC), market is in equilibrium / sideways
        var cpr = mtf?.DailyCpr;
        if (cpr != null && cpr.CprBottom > 0m && cpr.CprTop > 0m && spot >= cpr.CprBottom && spot <= cpr.CprTop)
        {
            return new MarketRegimeResult
            {
                Regime = MarketRegimeType.Sideways,
                Confidence = 80m,
                Description = $"Sideways Regime (CPR Equilibrium Trap): Spot ₹{spot:F2} is oscillating inside the Central Pivot Range (₹{cpr.CprBottom:F0} - ₹{cpr.CprTop:F0}). Directional option buying blocked.",
                AdxValue = adx15m,
                IndiaVix = vix,
                TrendAllowedBullish = false,
                TrendAllowedBearish = false,
                ReversalAllowed = false
            };
        }

        // 2. Strong Bullish Trend Regime
        // 15m ADX >= 22, 15m EMA 9 > 21, 5m & 15m Supertrend Bullish, Spot >= VWAP
        bool is15mBullish = snap15m != null && snap15m.Ema9 > snap15m.Ema21 && snap15m.SupertrendIsBullish == true;
        bool is5mBullish = snap5m != null && snap5m.Ema9 > snap5m.Ema21;
        bool isAboveVwap = snap5m?.Vwap != null && spot >= snap5m.Vwap.Value;

        if (adx15m >= 22m && is15mBullish && (is5mBullish || isAboveVwap))
        {
            decimal confidence = Math.Min(95m, 70m + (adx15m * 0.8m));
            return new MarketRegimeResult
            {
                Regime = MarketRegimeType.StrongBullish,
                Confidence = Math.Round(confidence, 1),
                Description = $"Strong Bullish Regime: 15m/5m trend alignment with ADX {adx15m:F1} confirming directional momentum.",
                AdxValue = adx15m,
                IndiaVix = vix,
                TrendAllowedBullish = true,
                TrendAllowedBearish = false,
                ReversalAllowed = false // Never fade a strong trend
            };
        }

        // 3. Strong Bearish Trend Regime
        bool is15mBearish = snap15m != null && snap15m.Ema9 < snap15m.Ema21 && snap15m.SupertrendIsBullish == false;
        bool is5mBearish = snap5m != null && snap5m.Ema9 < snap5m.Ema21;
        bool isBelowVwap = snap5m?.Vwap != null && spot <= snap5m.Vwap.Value;

        if (adx15m >= 22m && is15mBearish && (is5mBearish || isBelowVwap))
        {
            decimal confidence = Math.Min(95m, 70m + (adx15m * 0.8m));
            return new MarketRegimeResult
            {
                Regime = MarketRegimeType.StrongBearish,
                Confidence = Math.Round(confidence, 1),
                Description = $"Strong Bearish Regime: 15m/5m breakdown with ADX {adx15m:F1} confirming downward momentum.",
                AdxValue = adx15m,
                IndiaVix = vix,
                TrendAllowedBullish = false,
                TrendAllowedBearish = true,
                ReversalAllowed = false
            };
        }

        // 4. Sideways / Rangebound Regime
        // Weak ADX (< 20) or EMAs converged / price oscillating tightly around VWAP
        bool isAdxWeak = adx15m < 20m;
        bool isEmaFlat = snap15m != null && snap15m.Ema9.HasValue && snap15m.Ema21.HasValue &&
                         (Math.Abs(snap15m.Ema9.Value - snap15m.Ema21.Value) / spot) < 0.002m;

        if (isAdxWeak || isEmaFlat)
        {
            return new MarketRegimeResult
            {
                Regime = MarketRegimeType.Sideways,
                Confidence = 75m,
                Description = $"Sideways Regime: ADX {adx15m:F1} indicates lack of directional momentum. Mean reversion / range-bound conditions.",
                AdxValue = adx15m,
                IndiaVix = vix,
                TrendAllowedBullish = false,
                TrendAllowedBearish = false,
                ReversalAllowed = true // Reversals allowed at extreme support/resistance
            };
        }

        // 5. Transitional Regime (Mixed signals)
        return new MarketRegimeResult
        {
            Regime = MarketRegimeType.Transitional,
            Confidence = 60m,
            Description = "Transitional Regime: Conflicting timeframes or developing trend shift. Higher caution advised.",
            AdxValue = adx15m,
            IndiaVix = vix,
            TrendAllowedBullish = is15mBullish,
            TrendAllowedBearish = is15mBearish,
            ReversalAllowed = false
        };
    }
}
