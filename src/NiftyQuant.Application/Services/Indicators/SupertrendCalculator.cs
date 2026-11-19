using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Services.Indicators;

public static class SupertrendCalculator
{
    public static SupertrendResult Calculate(
        IReadOnlyList<MarketCandle> candles,
        int period = 10,
        decimal multiplier = 3.0m)
    {
        if (candles.Count < period || period <= 0)
        {
            return new SupertrendResult(null, false, null, null);
        }

        var atrSeries = AtrCalculator.CalculateAtrSeries(candles, period);
        if (atrSeries.Count == 0)
        {
            return new SupertrendResult(null, false, null, null);
        }

        // atrSeries index 0 corresponds to candles index (period - 1)
        int startIndex = period - 1;
        decimal prevFinalUpper = 0m;
        decimal prevFinalLower = 0m;
        decimal currentSupertrend = 0m;
        bool isBullish = true;

        for (int i = 0; i < atrSeries.Count; i++)
        {
            int candleIdx = startIndex + i;
            var candle = candles[candleIdx];
            var atr = atrSeries[i];

            decimal hl2 = (candle.High + candle.Low) / 2m;
            decimal basicUpper = hl2 + (multiplier * atr);
            decimal basicLower = hl2 - (multiplier * atr);

            decimal finalUpper;
            decimal finalLower;

            if (i == 0)
            {
                finalUpper = basicUpper;
                finalLower = basicLower;
                isBullish = candle.Close > finalUpper;
                currentSupertrend = isBullish ? finalLower : finalUpper;
            }
            else
            {
                var prevClose = candles[candleIdx - 1].Close;

                finalUpper = (basicUpper < prevFinalUpper || prevClose > prevFinalUpper)
                    ? basicUpper
                    : prevFinalUpper;

                finalLower = (basicLower > prevFinalLower || prevClose < prevFinalLower)
                    ? basicLower
                    : prevFinalLower;

                if (!isBullish)
                {
                    // Was Bearish
                    if (candle.Close > finalUpper)
                    {
                        isBullish = true;
                        currentSupertrend = finalLower;
                    }
                    else
                    {
                        isBullish = false;
                        currentSupertrend = finalUpper;
                    }
                }
                else
                {
                    // Was Bullish
                    if (candle.Close < finalLower)
                    {
                        isBullish = false;
                        currentSupertrend = finalUpper;
                    }
                    else
                    {
                        isBullish = true;
                        currentSupertrend = finalLower;
                    }
                }
            }

            prevFinalUpper = finalUpper;
            prevFinalLower = finalLower;
        }

        return new SupertrendResult(
            Math.Round(currentSupertrend, 2),
            isBullish,
            Math.Round(prevFinalUpper, 2),
            Math.Round(prevFinalLower, 2));
    }
}
