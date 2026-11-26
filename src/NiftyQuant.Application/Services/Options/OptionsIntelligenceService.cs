using System.Text;
using Microsoft.Extensions.Logging;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Services.Options;

public class OptionsIntelligenceService : IOptionsIntelligenceService
{
    private readonly ILogger<OptionsIntelligenceService> _logger;

    public OptionsIntelligenceService(ILogger<OptionsIntelligenceService> logger)
    {
        _logger = logger;
    }

    public OptionsIntelligenceReportDto AnalyzeSnapshot(
        OptionChainSnapshot snapshot,
        OptionChainSnapshot? previousSnapshot = null)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        var profile = IndexRegistry.GetProfile(snapshot.UnderlyingKey);
        var spotPrice = snapshot.UnderlyingPrice;
        var atmStrike = Math.Round(spotPrice / profile.StrikeInterval, MidpointRounding.AwayFromZero) * profile.StrikeInterval;

        // Group quotes by strike price
        var quotesByStrike = snapshot.Quotes
            .GroupBy(q => q.StrikePrice)
            .OrderBy(g => g.Key)
            .ToList();

        decimal totalCallVol = 0;
        decimal totalPutVol = 0;

        var strikeAnalysisList = new List<StrikeAnalysisDto>();

        foreach (var group in quotesByStrike)
        {
            var strike = group.Key;
            var call = group.FirstOrDefault(q => q.OptionType == OptionType.CE);
            var put = group.FirstOrDefault(q => q.OptionType == OptionType.PE);

            if (call != null) totalCallVol += call.Volume;
            if (put != null) totalPutVol += put.Volume;

            var callPriceChange = call != null ? call.Ltp - call.BidPrice : 0m; // or intraday
            var callOiChange = call?.IntradayOiChange ?? 0m;
            var callBuildup = ClassifyBuildup(call != null ? (call.Ltp - (call.PreviousOpenInterest > 0 ? call.Ltp * 0.98m : call.Ltp)) : 0m, callOiChange);

            var putPriceChange = put != null ? put.Ltp - put.BidPrice : 0m;
            var putOiChange = put?.IntradayOiChange ?? 0m;
            var putBuildup = ClassifyBuildup(put != null ? (put.Ltp - (put.PreviousOpenInterest > 0 ? put.Ltp * 0.98m : put.Ltp)) : 0m, putOiChange);

            decimal callSpread = 0m;
            if (call != null && call.Ltp > 0m && call.AskPrice >= call.BidPrice)
            {
                callSpread = Math.Round(((call.AskPrice - call.BidPrice) / call.Ltp) * 100m, 2);
            }

            decimal putSpread = 0m;
            if (put != null && put.Ltp > 0m && put.AskPrice >= put.BidPrice)
            {
                putSpread = Math.Round(((put.AskPrice - put.BidPrice) / put.Ltp) * 100m, 2);
            }

            strikeAnalysisList.Add(new StrikeAnalysisDto
            {
                StrikePrice = strike,
                IsAtm = strike == atmStrike,
                IsMajorCallResistance = strike == snapshot.HighestCallOiStrike,
                IsMajorPutSupport = strike == snapshot.HighestPutOiStrike,
                CallQuote = call,
                PutQuote = put,
                CallBuildup = callBuildup,
                PutBuildup = putBuildup,
                CallOi = call?.OpenInterest ?? 0m,
                PutOi = put?.OpenInterest ?? 0m,
                CallOiChange = callOiChange,
                PutOiChange = putOiChange,
                NetOiChange = putOiChange - callOiChange,
                CallDelta = call?.Delta,
                PutDelta = put?.Delta,
                CallIv = call?.ImpliedVolatility,
                PutIv = put?.ImpliedVolatility,
                CallVolume = call?.Volume ?? 0,
                PutVolume = put?.Volume ?? 0,
                CallBidAskSpreadPercent = callSpread,
                PutBidAskSpreadPercent = putSpread
            });
        }

        // 1. PCR Analysis
        var pcrAnalysis = AnalyzePcr(
            snapshot.TotalPutOi,
            snapshot.TotalCallOi,
            totalPutVol,
            totalCallVol,
            previousSnapshot?.Pcr);

        // 2. Support & Resistance Levels
        var maxPain = snapshot.MaxPainStrike ?? atmStrike;
        var levels = IdentifySupportResistance(spotPrice, maxPain, strikeAnalysisList);

        // 3. Overall Options Sentiment Score (-100 to +100)
        int sentimentScore = CalculateSentimentScore(pcrAnalysis, levels, strikeAnalysisList, spotPrice);
        var overallSentiment = GetSentimentEnum(sentimentScore);
        var sentimentSummary = BuildSentimentSummary(sentimentScore, overallSentiment, pcrAnalysis, levels);

        // 4. Contract Selection (CE & PE Buying)
        var recommendedCe = SelectBestContract(snapshot.Quotes, OptionType.CE, spotPrice, 40m, snapshot.UnderlyingKey);
        var recommendedPe = SelectBestContract(snapshot.Quotes, OptionType.PE, spotPrice, 40m, snapshot.UnderlyingKey);

        return new OptionsIntelligenceReportDto
        {
            UnderlyingKey = snapshot.UnderlyingKey,
            UnderlyingPrice = spotPrice,
            ExpiryDate = snapshot.ExpiryDate,
            SnapshotTimeUtc = snapshot.SnapshotTimeUtc,
            TotalStrikesAnalyzed = strikeAnalysisList.Count,
            PcrAnalysis = pcrAnalysis,
            KeyLevels = levels,
            OptionsSentimentScore = sentimentScore,
            OverallSentiment = overallSentiment,
            SentimentSummary = sentimentSummary,
            RecommendedCe = recommendedCe,
            RecommendedPe = recommendedPe,
            Strikes = strikeAnalysisList
        };
    }

    public OiBuildupType ClassifyBuildup(decimal priceChange, decimal oiChange)
    {
        // Ignore minor tick noise
        if (oiChange == 0m && priceChange == 0m)
        {
            return OiBuildupType.Neutral;
        }

        if (priceChange >= 0m && oiChange > 0m)
        {
            return OiBuildupType.LongBuildup;
        }

        if (priceChange <= 0m && oiChange > 0m)
        {
            return OiBuildupType.ShortBuildup;
        }

        if (priceChange >= 0m && oiChange < 0m)
        {
            return OiBuildupType.ShortCovering;
        }

        if (priceChange <= 0m && oiChange < 0m)
        {
            return OiBuildupType.LongUnwinding;
        }

        return OiBuildupType.Neutral;
    }

    public PcrAnalysisDto AnalyzePcr(
        decimal totalPutOi,
        decimal totalCallOi,
        decimal totalPutVol,
        decimal totalCallVol,
        decimal? previousPcr = null)
    {
        decimal pcrOi = totalCallOi > 0m ? Math.Round(totalPutOi / totalCallOi, 4) : 0m;
        decimal pcrVol = totalCallVol > 0m ? Math.Round(totalPutVol / totalCallVol, 4) : 0m;

        decimal? pcrChange = previousPcr.HasValue
            ? Math.Round(pcrOi - previousPcr.Value, 4)
            : null;

        string trend = "Flat";
        if (pcrChange.HasValue)
        {
            if (pcrChange.Value > 0.02m) trend = "Rising";
            else if (pcrChange.Value < -0.02m) trend = "Falling";
        }

        string sentimentZone;
        if (pcrOi > 1.40m) sentimentZone = "Overbought / Reversal Caution";
        else if (pcrOi > 1.15m) sentimentZone = "Strongly Bullish";
        else if (pcrOi >= 0.85m) sentimentZone = "Neutral";
        else if (pcrOi >= 0.70m) sentimentZone = "Bearish";
        else sentimentZone = "Oversold / Bounce Caution";

        return new PcrAnalysisDto
        {
            PcrOi = pcrOi,
            PcrVolume = pcrVol,
            PreviousPcrOi = previousPcr,
            PcrChange = pcrChange,
            PcrTrend = trend,
            SentimentZone = sentimentZone
        };
    }

    public SupportResistanceLevelsDto IdentifySupportResistance(
        decimal spotPrice,
        decimal maxPain,
        IReadOnlyList<StrikeAnalysisDto> strikes)
    {
        if (strikes.Count == 0)
        {
            return new SupportResistanceLevelsDto
            {
                SpotPrice = spotPrice,
                MaxPainStrike = maxPain,
                MajorCallResistance = spotPrice + 200m,
                MajorPutSupport = spotPrice - 200m,
                ExpectedTradingRangeLow = spotPrice - 200m,
                ExpectedTradingRangeHigh = spotPrice + 200m
            };
        }

        var majorCall = strikes.OrderByDescending(s => s.CallOi).FirstOrDefault()?.StrikePrice ?? spotPrice + 200m;
        var majorPut = strikes.OrderByDescending(s => s.PutOi).FirstOrDefault()?.StrikePrice ?? spotPrice - 200m;

        decimal avgCallOi = strikes.Average(s => s.CallOi);
        decimal avgPutOi = strikes.Average(s => s.PutOi);

        // Immediate Call Resistance: closest strike above spot with significant Call OI (>= 1.25x avg)
        var immCall = strikes
            .Where(s => s.StrikePrice >= spotPrice && s.CallOi >= (avgCallOi * 1.25m))
            .OrderBy(s => s.StrikePrice)
            .FirstOrDefault()?.StrikePrice;

        // Immediate Put Support: closest strike below spot with significant Put OI (>= 1.25x avg)
        var immPut = strikes
            .Where(s => s.StrikePrice <= spotPrice && s.PutOi >= (avgPutOi * 1.25m))
            .OrderByDescending(s => s.StrikePrice)
            .FirstOrDefault()?.StrikePrice;

        decimal rangeLow = immPut ?? majorPut;
        decimal rangeHigh = immCall ?? majorCall;

        return new SupportResistanceLevelsDto
        {
            SpotPrice = spotPrice,
            MaxPainStrike = maxPain,
            MajorCallResistance = majorCall,
            MajorPutSupport = majorPut,
            ImmediateCallResistance = immCall,
            ImmediatePutSupport = immPut,
            ExpectedTradingRangeLow = Math.Min(rangeLow, spotPrice - 50m),
            ExpectedTradingRangeHigh = Math.Max(rangeHigh, spotPrice + 50m)
        };
    }

    public SelectedOptionContractDto? SelectBestContract(
        IReadOnlyList<OptionQuote> quotes,
        OptionType optionType,
        decimal spotPrice,
        decimal minLiquidityScore = 40m,
        string underlyingKey = "NSE_INDEX|Nifty 50")
    {
        var targetQuotes = quotes
            .Where(q => q.OptionType == optionType && q.Ltp > 10m && q.AskPrice > 0m)
            .ToList();

        if (targetQuotes.Count == 0) return null;

        var sampleKey = targetQuotes.FirstOrDefault()?.InstrumentKey;
        var profile = IndexRegistry.GetProfile(underlyingKey);
        if (sampleKey != null && (sampleKey.Contains("BANK", StringComparison.OrdinalIgnoreCase) || sampleKey.Contains("SENSEX", StringComparison.OrdinalIgnoreCase)))
        {
            profile = IndexRegistry.GetProfile(sampleKey);
        }

        var maxOtmBuffer = profile.StrikeInterval * 0.3m;

        // Strictly prefer ATM and slight ITM contracts for Option Buying (eliminate OTM theta burn & zero intrinsic risk)
        var eligibleQuotes = targetQuotes.Where(q =>
        {
            if (optionType == OptionType.CE)
            {
                // Buy CE: Candidate strike must be ITM or true ATM (strike <= spot + maxOtmBuffer)
                return q.StrikePrice <= (spotPrice + maxOtmBuffer);
            }
            else
            {
                // Buy PE: Candidate strike must be ITM or true ATM (strike >= spot - maxOtmBuffer)
                return q.StrikePrice >= (spotPrice - maxOtmBuffer);
            }
        }).ToList();

        // Fallback to all target quotes only if no ITM/ATM quotes are present in quote list
        var quotesToEvaluate = eligibleQuotes.Count > 0 ? eligibleQuotes : targetQuotes;

        var atmStrike = Math.Round(spotPrice / profile.StrikeInterval, MidpointRounding.AwayFromZero) * profile.StrikeInterval;
        SelectedOptionContractDto? bestCandidate = null;
        decimal highestScore = -1m;

        foreach (var quote in quotesToEvaluate)
        {
            decimal score = 0m;
            var reasons = new List<string>();

            // 1. Delta Suitability Score (0 to 35)
            // Sweet spot: 0.50 to 0.60 (ATM / slight ITM)
            if (quote.Delta.HasValue && quote.Delta.Value != 0m)
            {
                var absDelta = Math.Abs(quote.Delta.Value);
                var deltaDistance = Math.Abs(absDelta - 0.54m); // target 0.54 (optimal ATM / slight ITM)
                var deltaScore = Math.Max(0m, 35m - (deltaDistance * 100m));

                if (absDelta >= 0.50m && absDelta <= 0.60m)
                {
                    deltaScore += 5m;
                }
                if (absDelta < 0.45m)
                {
                    deltaScore = Math.Max(0m, deltaScore - 20m);
                }

                score += deltaScore;
                reasons.Add($"Delta: {absDelta:F2} (+{deltaScore:F0})");
            }
            else
            {
                // Fallback to strike proximity to ATM
                var strikeDistance = Math.Abs(quote.StrikePrice - atmStrike);
                if (strikeDistance == 0m) 
                { 
                    score += 35m; 
                    reasons.Add("ATM (+35)"); 
                }
                else if (strikeDistance == profile.StrikeInterval) 
                { 
                    bool isItm = (optionType == OptionType.CE && quote.StrikePrice < spotPrice) ||
                                 (optionType == OptionType.PE && quote.StrikePrice > spotPrice);
                    if (isItm)
                    {
                        score += 30m;
                        reasons.Add("1-Strike ITM (+30)");
                    }
                    else
                    {
                        score += 20m;
                        reasons.Add("Near ATM (+20)");
                    }
                }
                else if (strikeDistance == profile.StrikeInterval * 2m) 
                { 
                    score += 10m; 
                    reasons.Add("Deeper ITM/OTM (+10)"); 
                }
            }

            // 2. Liquidity & Bid-Ask Spread Score (0 to 40)
            decimal spreadPct = quote.Ltp > 0m
                ? ((quote.AskPrice - quote.BidPrice) / quote.Ltp) * 100m
                : 100m;

            if (spreadPct <= 0.6m) { score += 25m; reasons.Add($"Tight Spread: {spreadPct:F2}% (+25)"); }
            else if (spreadPct <= 1.2m) { score += 20m; reasons.Add($"Moderate Spread: {spreadPct:F2}% (+20)"); }
            else if (spreadPct <= 2.0m) { score += 12m; reasons.Add($"Spread: {spreadPct:F2}% (+12)"); }
            else { reasons.Add($"Wide Spread: {spreadPct:F2}% (+0)"); }

            if (quote.Volume > 100000) { score += 15m; reasons.Add("High Volume (+15)"); }
            else if (quote.Volume > 25000) { score += 10m; reasons.Add("Good Volume (+10)"); }
            else if (quote.Volume > 5000) { score += 5m; reasons.Add("Moderate Volume (+5)"); }

            // 3. Open Interest Score (0 to 20)
            if (quote.OpenInterest > 1000000) { score += 20m; reasons.Add("Deep OI (+20)"); }
            else if (quote.OpenInterest > 300000) { score += 15m; reasons.Add("Strong OI (+15)"); }
            else if (quote.OpenInterest > 50000) { score += 8m; reasons.Add("Moderate OI (+8)"); }

            // 4. IV Suitability (0 to 10)
            if (quote.ImpliedVolatility.HasValue)
            {
                var iv = quote.ImpliedVolatility.Value;
                if (iv >= 10m && iv <= 22m) { score += 10m; reasons.Add($"Optimal IV {iv:F1}% (+10)"); }
                else if (iv < 32m) { score += 6m; reasons.Add($"Acceptable IV {iv:F1}% (+6)"); }
                else { reasons.Add($"High IV Risk {iv:F1}% (+0)"); }
            }
            else
            {
                score += 5m;
            }

            if (score >= minLiquidityScore && score > highestScore)
            {
                highestScore = score;
                bestCandidate = new SelectedOptionContractDto
                {
                    InstrumentKey = quote.InstrumentKey,
                    OptionType = quote.OptionType,
                    StrikePrice = quote.StrikePrice,
                    ExpiryDate = quote.ExpiryDate,
                    Ltp = quote.Ltp,
                    BidPrice = quote.BidPrice,
                    AskPrice = quote.AskPrice,
                    SpreadPercent = Math.Round(spreadPct, 2),
                    Volume = quote.Volume,
                    OpenInterest = quote.OpenInterest,
                    Delta = quote.Delta,
                    ImpliedVolatility = quote.ImpliedVolatility,
                    Theta = quote.Theta,
                    LiquidityScore = Math.Round(score, 1),
                    IsRecommended = true,
                    SelectionReason = string.Join(", ", reasons)
                };
            }
        }

        return bestCandidate;
    }

    public int CalculateSentimentScore(
        PcrAnalysisDto pcr,
        SupportResistanceLevelsDto levels,
        IReadOnlyList<StrikeAnalysisDto> strikes,
        decimal spotPrice)
    {
        int score = 0;

        // 1. PCR component (-25 to +25)
        if (pcr.PcrOi >= 1.30m) score += 25;
        else if (pcr.PcrOi >= 1.15m) score += 15;
        else if (pcr.PcrOi <= 0.65m) score -= 25;
        else if (pcr.PcrOi <= 0.85m) score -= 15;

        // 2. Spot vs Max Pain (-15 to +15)
        if (levels.MaxPainStrike > 0m)
        {
            var diff = spotPrice - levels.MaxPainStrike;
            if (diff >= 50m) score += 15;
            else if (diff <= -50m) score -= 15;
        }

        // 3. Near-ATM (within +-150 pts) Net OI delta component (-30 to +30)
        var nearAtmStrikes = strikes
            .Where(s => Math.Abs(s.StrikePrice - spotPrice) <= 150m)
            .ToList();

        if (nearAtmStrikes.Count > 0)
        {
            decimal nearPutOiDelta = nearAtmStrikes.Sum(s => s.PutOiChange);
            decimal nearCallOiDelta = nearAtmStrikes.Sum(s => s.CallOiChange);
            decimal netOiDiff = nearPutOiDelta - nearCallOiDelta;

            if (netOiDiff > 500000m) score += 30;
            else if (netOiDiff > 100000m) score += 18;
            else if (netOiDiff < -500000m) score -= 30;
            else if (netOiDiff < -100000m) score -= 18;

            // 4. Buildup patterns on ATM strikes (-30 to +30)
            int buildupScore = 0;
            foreach (var s in nearAtmStrikes)
            {
                if (s.CallBuildup == OiBuildupType.LongBuildup) buildupScore += 5;
                if (s.CallBuildup == OiBuildupType.ShortBuildup) buildupScore -= 5;
                if (s.PutBuildup == OiBuildupType.ShortBuildup) buildupScore += 5;
                if (s.PutBuildup == OiBuildupType.LongBuildup) buildupScore -= 5;
            }
            buildupScore = Math.Clamp(buildupScore, -30, 30);
            score += buildupScore;
        }

        return Math.Clamp(score, -100, 100);
    }

    private static OptionsSentiment GetSentimentEnum(int score) => score switch
    {
        >= 50 => OptionsSentiment.ExtremelyBullish,
        >= 15 => OptionsSentiment.Bullish,
        <= -50 => OptionsSentiment.ExtremelyBearish,
        <= -15 => OptionsSentiment.Bearish,
        _ => OptionsSentiment.Neutral
    };

    private static string BuildSentimentSummary(
        int score,
        OptionsSentiment sentiment,
        PcrAnalysisDto pcr,
        SupportResistanceLevelsDto levels)
    {
        var sb = new StringBuilder();
        sb.Append($"Options Sentiment: {sentiment} (Score: {score}/100). ");
        sb.Append($"PCR(OI): {pcr.PcrOi:F2} ({pcr.SentimentZone}, {pcr.PcrTrend}). ");
        sb.Append($"Max Pain: {levels.MaxPainStrike:F0}. ");
        sb.Append($"Range: Support {levels.MajorPutSupport:F0} to Resistance {levels.MajorCallResistance:F0}.");
        return sb.ToString();
    }
}
