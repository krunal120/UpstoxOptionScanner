using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Services.Indicators;

public static class RsiCalculator
{
    public static RsiResult Calculate(IReadOnlyList<MarketCandle> candles, int period = 14)
    {
        if (candles.Count <= period || period <= 0)
        {
            return new RsiResult(null);
        }

        // Calculate initial price changes
        decimal sumGain = 0m;
        decimal sumLoss = 0m;

        for (int i = 1; i <= period; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            if (change > 0m) sumGain += change;
            else if (change < 0m) sumLoss += -change;
        }

        decimal avgGain = sumGain / period;
        decimal avgLoss = sumLoss / period;

        // Wilder's smoothing for remaining candles
        for (int i = period + 1; i < candles.Count; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            decimal currentGain = change > 0m ? change : 0m;
            decimal currentLoss = change < 0m ? -change : 0m;

            avgGain = ((avgGain * (period - 1)) + currentGain) / period;
            avgLoss = ((avgLoss * (period - 1)) + currentLoss) / period;
        }

        decimal rsi;
        if (avgLoss == 0m)
        {
            rsi = avgGain == 0m ? 50m : 100m;
        }
        else
        {
            decimal rs = avgGain / avgLoss;
            rsi = 100m - (100m / (1m + rs));
        }

        return new RsiResult(Math.Round(rsi, 2));
    }
}
