using System.Text;
using Microsoft.Extensions.Logging;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Services.Indicators;

public class TechnicalIndicatorService : ITechnicalIndicatorService
{
    private readonly ILogger<TechnicalIndicatorService> _logger;

    public TechnicalIndicatorService(ILogger<TechnicalIndicatorService> logger)
    {
        _logger = logger;
    }

    public IndicatorSnapshotDto CalculateIndicators(
        IReadOnlyList<MarketCandle> candles,
        CandleTimeframe timeframe,
        CprResult? dailyCpr = null)
    {
        if (candles == null || candles.Count == 0)
        {
            return new IndicatorSnapshotDto
            {
                Timeframe = timeframe,
                DailyCpr = dailyCpr
            };
        }

        var orderedCandles = candles.OrderBy(c => c.CandleTimeUtc).ToList();
        var latestCandle = orderedCandles[^1];
        var closes = orderedCandles.Select(c => c.Close).ToList();
        var volumes = orderedCandles.Select(c => (decimal)c.Volume).ToList();

        // 1. EMAs
        var ema9 = MovingAverageCalculator.CalculateLatestEma(closes, 9);
        var ema21 = MovingAverageCalculator.CalculateLatestEma(closes, 21);
        var ema50 = MovingAverageCalculator.CalculateLatestEma(closes, 50);
        var ema200 = MovingAverageCalculator.CalculateLatestEma(closes, 200);

        // 2. VWAP
        var vwapResult = VwapCalculator.Calculate(orderedCandles);

        // 3. Supertrend (10, 3.0)
        var supertrend = SupertrendCalculator.Calculate(orderedCandles, period: 10, multiplier: 3.0m);

        // 4. RSI (14)
        var rsi = RsiCalculator.Calculate(orderedCandles, period: 14);

        // 5. MACD (12, 26, 9)
        var macd = MacdCalculator.Calculate(orderedCandles, fastPeriod: 12, slowPeriod: 26, signalPeriod: 9);

        // 6. ADX (14)
        var adx = AdxCalculator.Calculate(orderedCandles, period: 14);

        // 7. ATR (14)
        var atr = AtrCalculator.CalculateLatestAtr(orderedCandles, period: 14);

        // 8. Bollinger Bands (20, 2.0)
        var bb = BollingerBandsCalculator.Calculate(orderedCandles, period: 20, stdDevMultiplier: 2.0m);

        // 9. Volume Moving Average & Relative Volume
        var volSma20 = MovingAverageCalculator.CalculateSma(volumes, 20);
        decimal? rVol = null;
        if (volSma20.HasValue && volSma20.Value > 0m)
        {
            rVol = Math.Round((decimal)latestCandle.Volume / volSma20.Value, 2);
        }

        return new IndicatorSnapshotDto
        {
            InstrumentKey = latestCandle.InstrumentKey,
            Timeframe = timeframe,
            CandleTimeUtc = latestCandle.CandleTimeUtc,
            ClosePrice = latestCandle.Close,
            Ema9 = ema9,
            Ema21 = ema21,
            Ema50 = ema50,
            Ema200 = ema200,
            Vwap = vwapResult.Vwap,
            Supertrend = supertrend.Supertrend,
            SupertrendIsBullish = supertrend.IsBullish,
            Rsi14 = rsi.Rsi,
            Macd = macd.MacdLine,
            MacdSignal = macd.SignalLine,
            MacdHistogram = macd.Histogram,
            Adx14 = adx.Adx,
            PlusDi = adx.PlusDi,
            MinusDi = adx.MinusDi,
            Atr14 = atr,
            BollingerUpper = bb.UpperBand,
            BollingerMiddle = bb.MiddleBand,
            BollingerLower = bb.LowerBand,
            BollingerBandwidth = bb.Bandwidth,
            VolumeSma20 = volSma20,
            RelativeVolume = rVol,
            DailyCpr = dailyCpr
        };
    }

    public EmaResult CalculateEmas(IReadOnlyList<MarketCandle> candles)
    {
        var closes = candles.OrderBy(c => c.CandleTimeUtc).Select(c => c.Close).ToList();
        return new EmaResult(
            MovingAverageCalculator.CalculateLatestEma(closes, 9),
            MovingAverageCalculator.CalculateLatestEma(closes, 21),
            MovingAverageCalculator.CalculateLatestEma(closes, 50),
            MovingAverageCalculator.CalculateLatestEma(closes, 200));
    }

    public RsiResult CalculateRsi(IReadOnlyList<MarketCandle> candles, int period = 14)
    {
        var ordered = candles.OrderBy(c => c.CandleTimeUtc).ToList();
        return RsiCalculator.Calculate(ordered, period);
    }

    public MacdResult CalculateMacd(
        IReadOnlyList<MarketCandle> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        var ordered = candles.OrderBy(c => c.CandleTimeUtc).ToList();
        return MacdCalculator.Calculate(ordered, fastPeriod, slowPeriod, signalPeriod);
    }

    public SupertrendResult CalculateSupertrend(
        IReadOnlyList<MarketCandle> candles,
        int period = 10,
        decimal multiplier = 3.0m)
    {
        var ordered = candles.OrderBy(c => c.CandleTimeUtc).ToList();
        return SupertrendCalculator.Calculate(ordered, period, multiplier);
    }

    public AdxResult CalculateAdx(IReadOnlyList<MarketCandle> candles, int period = 14)
    {
        var ordered = candles.OrderBy(c => c.CandleTimeUtc).ToList();
        return AdxCalculator.Calculate(ordered, period);
    }

    public decimal? CalculateAtr(IReadOnlyList<MarketCandle> candles, int period = 14)
    {
        var ordered = candles.OrderBy(c => c.CandleTimeUtc).ToList();
        return AtrCalculator.CalculateLatestAtr(ordered, period);
    }

    public BollingerBandsResult CalculateBollingerBands(
        IReadOnlyList<MarketCandle> candles,
        int period = 20,
        decimal stdDevMultiplier = 2.0m)
    {
        var ordered = candles.OrderBy(c => c.CandleTimeUtc).ToList();
        return BollingerBandsCalculator.Calculate(ordered, period, stdDevMultiplier);
    }

    public VwapResult CalculateVwap(IReadOnlyList<MarketCandle> candles)
    {
        var ordered = candles.OrderBy(c => c.CandleTimeUtc).ToList();
        return VwapCalculator.Calculate(ordered);
    }

    public CprResult CalculateDailyCpr(decimal high, decimal low, decimal close)
    {
        return CprCalculator.Calculate(high, low, close);
    }

    public MultiTimeframeIndicatorState CalculateMultiTimeframeState(
        string instrumentKey,
        IReadOnlyList<MarketCandle> candles1m,
        IReadOnlyList<MarketCandle> candles5m,
        IReadOnlyList<MarketCandle> candles15m,
        CprResult? dailyCpr = null)
    {
        var snap1m = candles1m.Count > 0 ? CalculateIndicators(candles1m, CandleTimeframe.OneMinute, dailyCpr) : null;
        var snap5m = candles5m.Count > 0 ? CalculateIndicators(candles5m, CandleTimeframe.FiveMinute, dailyCpr) : null;
        var snap15m = candles15m.Count > 0 ? CalculateIndicators(candles15m, CandleTimeframe.FifteenMinute, dailyCpr) : null;

        decimal currentPrice = snap1m?.ClosePrice ?? snap5m?.ClosePrice ?? snap15m?.ClosePrice ?? 0m;

        // Check EMA Trend alignment: Bullish if 9 > 21 across 15m, 5m, 1m
        bool trend15mBullish = snap15m != null && snap15m.Ema9.HasValue && snap15m.Ema21.HasValue && snap15m.Ema9 > snap15m.Ema21;
        bool trend5mBullish = snap5m != null && snap5m.Ema9.HasValue && snap5m.Ema21.HasValue && snap5m.Ema9 > snap5m.Ema21;
        bool trend1mBullish = snap1m != null && snap1m.Ema9.HasValue && snap1m.Ema21.HasValue && snap1m.Ema9 > snap1m.Ema21;

        bool trend15mBearish = snap15m != null && snap15m.Ema9.HasValue && snap15m.Ema21.HasValue && snap15m.Ema9 < snap15m.Ema21;
        bool trend5mBearish = snap5m != null && snap5m.Ema9.HasValue && snap5m.Ema21.HasValue && snap5m.Ema9 < snap5m.Ema21;
        bool trend1mBearish = snap1m != null && snap1m.Ema9.HasValue && snap1m.Ema21.HasValue && snap1m.Ema9 < snap1m.Ema21;

        bool trendAlignedBullish = trend15mBullish && trend5mBullish && trend1mBullish;
        bool trendAlignedBearish = trend15mBearish && trend5mBearish && trend1mBearish;

        // Supertrend alignment
        bool st15mBullish = snap15m?.SupertrendIsBullish == true;
        bool st5mBullish = snap5m?.SupertrendIsBullish == true;
        bool st1mBullish = snap1m?.SupertrendIsBullish == true;

        bool supertrendAlignedBullish = st15mBullish && st5mBullish && st1mBullish;
        bool supertrendAlignedBearish = (snap15m?.SupertrendIsBullish == false) &&
                                        (snap5m?.SupertrendIsBullish == false) &&
                                        (snap1m?.SupertrendIsBullish == false);

        // VWAP alignment
        var validVwaps = new[] { snap1m?.Vwap, snap5m?.Vwap, snap15m?.Vwap }
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        bool aboveVwapAcrossAll = validVwaps.Count > 0 && validVwaps.All(v => currentPrice >= v);
        bool belowVwapAcrossAll = validVwaps.Count > 0 && validVwaps.All(v => currentPrice <= v);

        // Summary builder
        var summary = new StringBuilder();
        if (trendAlignedBullish && supertrendAlignedBullish && aboveVwapAcrossAll)
        {
            summary.Append("STRONG BULLISH ALIGNMENT: 15m, 5m, 1m trends, Supertrends, and VWAP all aligned bullish.");
        }
        else if (trendAlignedBearish && supertrendAlignedBearish && belowVwapAcrossAll)
        {
            summary.Append("STRONG BEARISH ALIGNMENT: 15m, 5m, 1m trends, Supertrends, and VWAP all aligned bearish.");
        }
        else if (trend15mBullish && (trend5mBullish || st5mBullish))
        {
            summary.Append("MODERATELY BULLISH: Higher timeframes bullish, awaiting 1m trigger.");
        }
        else if (trend15mBearish && (trend5mBearish || !st5mBullish))
        {
            summary.Append("MODERATELY BEARISH: Higher timeframes bearish, awaiting 1m trigger.");
        }
        else
        {
            summary.Append("SIDEWAYS / MIXED: Conflicting multi-timeframe signals.");
        }

        return new MultiTimeframeIndicatorState
        {
            InstrumentKey = instrumentKey,
            TimestampUtc = DateTime.UtcNow,
            CurrentPrice = currentPrice,
            Timeframe1m = snap1m,
            Timeframe5m = snap5m,
            Timeframe15m = snap15m,
            DailyCpr = dailyCpr,
            TrendAlignedBullish = trendAlignedBullish,
            TrendAlignedBearish = trendAlignedBearish,
            SupertrendAlignedBullish = supertrendAlignedBullish,
            SupertrendAlignedBearish = supertrendAlignedBearish,
            AboveVwapAcrossAll = aboveVwapAcrossAll,
            BelowVwapAcrossAll = belowVwapAcrossAll,
            AlignmentSummary = summary.ToString()
        };
    }
}
