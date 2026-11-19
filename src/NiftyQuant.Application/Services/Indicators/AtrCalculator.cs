using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Services.Indicators;

public static class AtrCalculator
{
    public static List<decimal> CalculateTrueRangeSeries(IReadOnlyList<MarketCandle> candles)
    {
        var trList = new List<decimal>(candles.Count);
        if (candles.Count == 0) return trList;

        trList.Add(candles[0].High - candles[0].Low);

        for (int i = 1; i < candles.Count; i++)
        {
            var high = candles[i].High;
            var low = candles[i].Low;
            var prevClose = candles[i - 1].Close;

            var hl = high - low;
            var hc = Math.Abs(high - prevClose);
            var lc = Math.Abs(low - prevClose);

            var tr = Math.Max(hl, Math.Max(hc, lc));
            trList.Add(tr);
        }

        return trList;
    }

    public static List<decimal> CalculateAtrSeries(IReadOnlyList<MarketCandle> candles, int period = 14)
    {
        var atrSeries = new List<decimal>();
        if (candles.Count < period || period <= 0) return atrSeries;

        var trSeries = CalculateTrueRangeSeries(candles);

        // First ATR is the simple average of the first 'period' TR values
        decimal sum = 0m;
        for (int i = 0; i < period; i++)
        {
            sum += trSeries[i];
        }
        decimal currentAtr = sum / period;
        atrSeries.Add(Math.Round(currentAtr, 4));

        // Subsequent ATRs use Wilder's Smoothing: (Prior ATR * (period - 1) + Current TR) / period
        for (int i = period; i < trSeries.Count; i++)
        {
            currentAtr = ((currentAtr * (period - 1)) + trSeries[i]) / period;
            atrSeries.Add(Math.Round(currentAtr, 4));
        }

        return atrSeries;
    }

    public static decimal? CalculateLatestAtr(IReadOnlyList<MarketCandle> candles, int period = 14)
    {
        var series = CalculateAtrSeries(candles, period);
        return series.Count > 0 ? series[^1] : null;
    }
}
