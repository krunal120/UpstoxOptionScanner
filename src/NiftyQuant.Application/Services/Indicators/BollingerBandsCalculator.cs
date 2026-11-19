using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Services.Indicators;

public static class BollingerBandsCalculator
{
    public static BollingerBandsResult Calculate(
        IReadOnlyList<MarketCandle> candles,
        int period = 20,
        decimal stdDevMultiplier = 2.0m)
    {
        if (candles.Count < period || period <= 0)
        {
            return new BollingerBandsResult(null, null, null, null, null);
        }

        decimal sum = 0m;
        int startIndex = candles.Count - period;

        for (int i = startIndex; i < candles.Count; i++)
        {
            sum += candles[i].Close;
        }

        decimal middleBand = sum / period;

        // Calculate sample variance and standard deviation
        double varianceSum = 0;
        for (int i = startIndex; i < candles.Count; i++)
        {
            double diff = (double)(candles[i].Close - middleBand);
            varianceSum += diff * diff;
        }

        decimal stdDev = (decimal)Math.Sqrt(varianceSum / period);

        decimal upperBand = middleBand + (stdDevMultiplier * stdDev);
        decimal lowerBand = middleBand - (stdDevMultiplier * stdDev);

        decimal bandwidth = middleBand > 0m
            ? Math.Round(((upperBand - lowerBand) / middleBand) * 100m, 2)
            : 0m;

        var latestClose = candles[^1].Close;
        decimal bandRange = upperBand - lowerBand;
        decimal percentB = bandRange > 0m
            ? Math.Round((latestClose - lowerBand) / bandRange, 4)
            : 0.5m;

        return new BollingerBandsResult(
            Math.Round(upperBand, 2),
            Math.Round(middleBand, 2),
            Math.Round(lowerBand, 2),
            bandwidth,
            percentB);
    }
}
