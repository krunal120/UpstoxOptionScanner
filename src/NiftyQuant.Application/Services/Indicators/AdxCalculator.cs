using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Services.Indicators;

public static class AdxCalculator
{
    public static AdxResult Calculate(IReadOnlyList<MarketCandle> candles, int period = 14)
    {
        if (candles.Count <= period || period <= 0)
        {
            return new AdxResult(null, null, null);
        }

        var trList = new List<decimal>(candles.Count - 1);
        var plusDmList = new List<decimal>(candles.Count - 1);
        var minusDmList = new List<decimal>(candles.Count - 1);

        for (int i = 1; i < candles.Count; i++)
        {
            var high = candles[i].High;
            var low = candles[i].Low;
            var prevHigh = candles[i - 1].High;
            var prevLow = candles[i - 1].Low;
            var prevClose = candles[i - 1].Close;

            var hl = high - low;
            var hc = Math.Abs(high - prevClose);
            var lc = Math.Abs(low - prevClose);
            var tr = Math.Max(hl, Math.Max(hc, lc));
            trList.Add(tr);

            var upMove = high - prevHigh;
            var downMove = prevLow - low;

            decimal plusDm = (upMove > downMove && upMove > 0m) ? upMove : 0m;
            decimal minusDm = (downMove > upMove && downMove > 0m) ? downMove : 0m;

            plusDmList.Add(plusDm);
            minusDmList.Add(minusDm);
        }

        if (trList.Count < period)
        {
            return new AdxResult(null, null, null);
        }

        // Sum first 'period' values
        decimal smoothedTr = 0m;
        decimal smoothedPlusDm = 0m;
        decimal smoothedMinusDm = 0m;

        for (int i = 0; i < period; i++)
        {
            smoothedTr += trList[i];
            smoothedPlusDm += plusDmList[i];
            smoothedMinusDm += minusDmList[i];
        }

        var dxList = new List<decimal>();
        decimal currentPlusDi = smoothedTr > 0m ? (smoothedPlusDm / smoothedTr) * 100m : 0m;
        decimal currentMinusDi = smoothedTr > 0m ? (smoothedMinusDm / smoothedTr) * 100m : 0m;
        decimal diSum = currentPlusDi + currentMinusDi;
        decimal initialDx = diSum > 0m ? (Math.Abs(currentPlusDi - currentMinusDi) / diSum) * 100m : 0m;
        dxList.Add(initialDx);

        // Process remaining entries using Wilder's smoothing
        for (int i = period; i < trList.Count; i++)
        {
            smoothedTr = smoothedTr - (smoothedTr / period) + trList[i];
            smoothedPlusDm = smoothedPlusDm - (smoothedPlusDm / period) + plusDmList[i];
            smoothedMinusDm = smoothedMinusDm - (smoothedMinusDm / period) + minusDmList[i];

            currentPlusDi = smoothedTr > 0m ? (smoothedPlusDm / smoothedTr) * 100m : 0m;
            currentMinusDi = smoothedTr > 0m ? (smoothedMinusDm / smoothedTr) * 100m : 0m;

            diSum = currentPlusDi + currentMinusDi;
            decimal dx = diSum > 0m ? (Math.Abs(currentPlusDi - currentMinusDi) / diSum) * 100m : 0m;
            dxList.Add(dx);
        }

        decimal? finalAdx = null;
        if (dxList.Count >= period)
        {
            decimal sumDx = 0m;
            for (int i = 0; i < period; i++)
            {
                sumDx += dxList[i];
            }
            decimal adx = sumDx / period;

            for (int i = period; i < dxList.Count; i++)
            {
                adx = ((adx * (period - 1)) + dxList[i]) / period;
            }
            finalAdx = Math.Round(adx, 2);
        }
        else if (dxList.Count > 0)
        {
            // If less than 2*period, provide average DX
            finalAdx = Math.Round(dxList.Average(), 2);
        }

        return new AdxResult(
            finalAdx,
            Math.Round(currentPlusDi, 2),
            Math.Round(currentMinusDi, 2));
    }
}
