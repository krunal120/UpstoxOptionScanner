using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Models.Indicators;

public record EmaResult(decimal? Ema9, decimal? Ema21, decimal? Ema50, decimal? Ema200);

public record MacdResult(decimal? MacdLine, decimal? SignalLine, decimal? Histogram);

public record RsiResult(decimal? Rsi);

public record SupertrendResult(decimal? Supertrend, bool IsBullish, decimal? UpperBand, decimal? LowerBand);

public record AdxResult(decimal? Adx, decimal? PlusDi, decimal? MinusDi);

public record BollingerBandsResult(decimal? UpperBand, decimal? MiddleBand, decimal? LowerBand, decimal? Bandwidth, decimal? PercentB);

public record CprResult(
    decimal Pivot,
    decimal BottomCentral,
    decimal TopCentral,
    decimal CprTop,
    decimal CprBottom,
    decimal CprWidth,
    decimal CprWidthPercent,
    bool IsNarrowCpr,
    decimal R1,
    decimal S1,
    decimal R2,
    decimal S2,
    decimal R3,
    decimal S3);

public record VwapResult(decimal? Vwap, decimal? UpperBand1Dev, decimal? LowerBand1Dev);

public class IndicatorSnapshotDto
{
    public string InstrumentKey { get; set; } = string.Empty;
    public CandleTimeframe Timeframe { get; set; }
    public DateTime CandleTimeUtc { get; set; }
    public decimal ClosePrice { get; set; }

    // EMAs
    public decimal? Ema9 { get; set; }
    public decimal? Ema21 { get; set; }
    public decimal? Ema50 { get; set; }
    public decimal? Ema200 { get; set; }

    // Momentum & Trend
    public decimal? Vwap { get; set; }
    public decimal? Supertrend { get; set; }
    public bool? SupertrendIsBullish { get; set; }
    public decimal? Rsi14 { get; set; }
    public decimal? Macd { get; set; }
    public decimal? MacdSignal { get; set; }
    public decimal? MacdHistogram { get; set; }
    public decimal? Adx14 { get; set; }
    public decimal? PlusDi { get; set; }
    public decimal? MinusDi { get; set; }
    public decimal? Atr14 { get; set; }

    // Volatility
    public decimal? BollingerUpper { get; set; }
    public decimal? BollingerMiddle { get; set; }
    public decimal? BollingerLower { get; set; }
    public decimal? BollingerBandwidth { get; set; }

    // Volume
    public decimal? VolumeSma20 { get; set; }
    public decimal? RelativeVolume { get; set; }

    // CPR Levels
    public CprResult? DailyCpr { get; set; }
}

public class MultiTimeframeIndicatorState
{
    public string InstrumentKey { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public decimal CurrentPrice { get; set; }

    public IndicatorSnapshotDto? Timeframe1m { get; set; }
    public IndicatorSnapshotDto? Timeframe5m { get; set; }
    public IndicatorSnapshotDto? Timeframe15m { get; set; }
    public CprResult? DailyCpr { get; set; }

    // Multi-Timeframe Alignment Flags
    public bool TrendAlignedBullish { get; set; }
    public bool TrendAlignedBearish { get; set; }
    public bool SupertrendAlignedBullish { get; set; }
    public bool SupertrendAlignedBearish { get; set; }
    public bool AboveVwapAcrossAll { get; set; }
    public bool BelowVwapAcrossAll { get; set; }
    public string AlignmentSummary { get; set; } = string.Empty;
}
