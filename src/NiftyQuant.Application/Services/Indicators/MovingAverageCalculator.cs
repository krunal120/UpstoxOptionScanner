namespace NiftyQuant.Application.Services.Indicators;

public static class MovingAverageCalculator
{
    public static decimal? CalculateSma(IReadOnlyList<decimal> prices, int period)
    {
        if (prices.Count < period || period <= 0) return null;

        decimal sum = 0m;
        for (int i = prices.Count - period; i < prices.Count; i++)
        {
            sum += prices[i];
        }

        return Math.Round(sum / period, 4);
    }

    public static List<decimal> CalculateEmaSeries(IReadOnlyList<decimal> prices, int period)
    {
        var result = new List<decimal>();
        if (prices.Count == 0 || period <= 0) return result;

        if (prices.Count < period)
        {
            // Not enough prices for a full period EMA
            return result;
        }

        decimal multiplier = 2m / (period + 1);

        // Seed with SMA of the first 'period' items
        decimal sum = 0m;
        for (int i = 0; i < period; i++)
        {
            sum += prices[i];
        }
        decimal currentEma = sum / period;
        result.Add(Math.Round(currentEma, 4));

        for (int i = period; i < prices.Count; i++)
        {
            currentEma = (prices[i] * multiplier) + (currentEma * (1m - multiplier));
            result.Add(Math.Round(currentEma, 4));
        }

        return result;
    }

    public static decimal? CalculateLatestEma(IReadOnlyList<decimal> prices, int period)
    {
        var series = CalculateEmaSeries(prices, period);
        return series.Count > 0 ? series[^1] : null;
    }
}
