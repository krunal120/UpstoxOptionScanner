using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Domain.Entities;

public class IndicatorSnapshot
{
    public long Id { get; set; }
    public string InstrumentKey { get; set; } = "NSE_INDEX|Nifty 50";
    public CandleTimeframe Timeframe { get; set; }
    public DateTime CandleTimeUtc { get; set; }
    public decimal? Ema9 { get; set; }
    public decimal? Ema21 { get; set; }
    public decimal? Ema50 { get; set; }
    public decimal? Ema200 { get; set; }
    public decimal? Vwap { get; set; }
    public decimal? Supertrend { get; set; }
    public bool? SupertrendDirectionIsBullish { get; set; }
    public decimal? Rsi14 { get; set; }
    public decimal? Macd { get; set; }
    public decimal? MacdSignal { get; set; }
    public decimal? MacdHistogram { get; set; }
    public decimal? Adx14 { get; set; }
    public decimal? PlusDi { get; set; }
    public decimal? MinusDi { get; set; }
    public decimal? Atr14 { get; set; }
    public decimal? BollingerUpper { get; set; }
    public decimal? BollingerMiddle { get; set; }
    public decimal? BollingerLower { get; set; }
    public decimal? RelativeVolume { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class MarketRegimeRecord
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public MarketRegimeType RegimeType { get; set; }
    public decimal Confidence { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal AdxValue { get; set; }
    public bool PriceAboveVwap { get; set; }
    public bool EmaBullishCross { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
