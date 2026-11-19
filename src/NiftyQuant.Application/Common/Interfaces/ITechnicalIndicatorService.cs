using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Interfaces;

public interface ITechnicalIndicatorService
{
    IndicatorSnapshotDto CalculateIndicators(
        IReadOnlyList<MarketCandle> candles,
        CandleTimeframe timeframe,
        CprResult? dailyCpr = null);

    EmaResult CalculateEmas(IReadOnlyList<MarketCandle> candles);

    RsiResult CalculateRsi(IReadOnlyList<MarketCandle> candles, int period = 14);

    MacdResult CalculateMacd(
        IReadOnlyList<MarketCandle> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9);

    SupertrendResult CalculateSupertrend(
        IReadOnlyList<MarketCandle> candles,
        int period = 10,
        decimal multiplier = 3.0m);

    AdxResult CalculateAdx(IReadOnlyList<MarketCandle> candles, int period = 14);

    decimal? CalculateAtr(IReadOnlyList<MarketCandle> candles, int period = 14);

    BollingerBandsResult CalculateBollingerBands(
        IReadOnlyList<MarketCandle> candles,
        int period = 20,
        decimal stdDevMultiplier = 2.0m);

    VwapResult CalculateVwap(IReadOnlyList<MarketCandle> candles);

    CprResult CalculateDailyCpr(decimal high, decimal low, decimal close);

    MultiTimeframeIndicatorState CalculateMultiTimeframeState(
        string instrumentKey,
        IReadOnlyList<MarketCandle> candles1m,
        IReadOnlyList<MarketCandle> candles5m,
        IReadOnlyList<MarketCandle> candles15m,
        CprResult? dailyCpr = null);
}
