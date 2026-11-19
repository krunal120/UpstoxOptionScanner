using NiftyQuant.Application.Common.Models.Indicators;

namespace NiftyQuant.Application.Services.Indicators;

public static class CprCalculator
{
    public static CprResult Calculate(decimal high, decimal low, decimal close)
    {
        decimal pivot = Math.Round((high + low + close) / 3m, 2);
        decimal bc = Math.Round((high + low) / 2m, 2);
        decimal tc = Math.Round((2m * pivot) - bc, 2);

        decimal cprTop = Math.Max(tc, bc);
        decimal cprBottom = Math.Min(tc, bc);
        decimal cprWidth = Math.Round(Math.Abs(tc - bc), 2);
        decimal cprWidthPercent = pivot > 0m
            ? Math.Round((cprWidth / pivot) * 100m, 4)
            : 0m;

        // Standard Indian market benchmark: CPR width <= 0.15% is classified as Narrow CPR (trend day)
        bool isNarrowCpr = cprWidthPercent <= 0.15m;

        decimal r1 = Math.Round((2m * pivot) - low, 2);
        decimal s1 = Math.Round((2m * pivot) - high, 2);
        decimal r2 = Math.Round(pivot + (high - low), 2);
        decimal s2 = Math.Round(pivot - (high - low), 2);
        decimal r3 = Math.Round(high + 2m * (pivot - low), 2);
        decimal s3 = Math.Round(low - 2m * (high - pivot), 2);

        return new CprResult(
            pivot,
            bc,
            tc,
            cprTop,
            cprBottom,
            cprWidth,
            cprWidthPercent,
            isNarrowCpr,
            r1,
            s1,
            r2,
            s2,
            r3,
            s3);
    }
}
