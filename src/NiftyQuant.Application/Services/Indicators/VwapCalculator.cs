using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Services.Indicators;

public static class VwapCalculator
{
    private static readonly TimeZoneInfo IstZone = GetIstTimeZone();

    public static VwapResult Calculate(IReadOnlyList<MarketCandle> candles)
    {
        if (candles.Count == 0)
        {
            return new VwapResult(null, null, null);
        }

        // Filter candles to the latest trading session (IST date)
        var utcLast = DateTime.SpecifyKind(candles[^1].CandleTimeUtc, DateTimeKind.Utc);
        var latestCandleIst = TimeZoneInfo.ConvertTimeFromUtc(utcLast, IstZone);
        var sessionDate = latestCandleIst.Date;

        var sessionCandles = candles
            .Where(c => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(c.CandleTimeUtc, DateTimeKind.Utc), IstZone).Date == sessionDate)
            .ToList();

        if (sessionCandles.Count == 0)
        {
            sessionCandles = candles.ToList();
        }

        decimal sumTpv = 0m;
        long sumVolume = 0;

        foreach (var c in sessionCandles)
        {
            decimal tp = (c.High + c.Low + c.Close) / 3m;
            long vol = c.Volume > 0 ? c.Volume : 1; // Graceful fallback if volume is 0

            sumTpv += tp * vol;
            sumVolume += vol;
        }

        if (sumVolume == 0)
        {
            decimal close = candles[^1].Close;
            return new VwapResult(Math.Round(close, 2), Math.Round(close, 2), Math.Round(close, 2));
        }

        decimal vwap = sumTpv / sumVolume;

        // Calculate standard deviation around VWAP
        double weightedVarianceSum = 0;
        foreach (var c in sessionCandles)
        {
            decimal tp = (c.High + c.Low + c.Close) / 3m;
            long vol = c.Volume > 0 ? c.Volume : 1;
            double diff = (double)(tp - vwap);
            weightedVarianceSum += diff * diff * vol;
        }

        decimal stdDev = (decimal)Math.Sqrt(weightedVarianceSum / sumVolume);
        decimal upper1 = vwap + stdDev;
        decimal lower1 = vwap - stdDev;

        return new VwapResult(
            Math.Round(vwap, 2),
            Math.Round(upper1, 2),
            Math.Round(lower1, 2));
    }

    private static TimeZoneInfo GetIstTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
    }
}
