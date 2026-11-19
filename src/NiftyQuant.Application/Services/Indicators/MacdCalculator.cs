using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Services.Indicators;

public static class MacdCalculator
{
    public static MacdResult Calculate(
        IReadOnlyList<MarketCandle> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        if (candles.Count < slowPeriod || fastPeriod >= slowPeriod || signalPeriod <= 0)
        {
            return new MacdResult(null, null, null);
        }

        var closes = candles.Select(c => c.Close).ToList();

        var fastEmaSeries = MovingAverageCalculator.CalculateEmaSeries(closes, fastPeriod);
        var slowEmaSeries = MovingAverageCalculator.CalculateEmaSeries(closes, slowPeriod);

        if (slowEmaSeries.Count == 0)
        {
            return new MacdResult(null, null, null);
        }

        // Align fast and slow series:
        // slowEmaSeries[0] corresponds to closes[slowPeriod - 1]
        // fastEmaSeries offset = slowPeriod - fastPeriod
        int offset = slowPeriod - fastPeriod;
        var macdSeries = new List<decimal>(slowEmaSeries.Count);

        for (int i = 0; i < slowEmaSeries.Count; i++)
        {
            decimal macdVal = fastEmaSeries[i + offset] - slowEmaSeries[i];
            macdSeries.Add(macdVal);
        }

        decimal latestMacd = macdSeries[^1];

        var signalSeries = MovingAverageCalculator.CalculateEmaSeries(macdSeries, signalPeriod);
        decimal? latestSignal = signalSeries.Count > 0 ? signalSeries[^1] : null;

        decimal? histogram = latestSignal.HasValue
            ? Math.Round(latestMacd - latestSignal.Value, 4)
            : null;

        return new MacdResult(
            Math.Round(latestMacd, 4),
            latestSignal.HasValue ? Math.Round(latestSignal.Value, 4) : null,
            histogram);
    }
}
