using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Domain.Entities;

public class Instrument
{
    public string InstrumentKey { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public string Segment { get; set; } = "INDEX";
    public int LotSize { get; set; } = 50;
    public decimal TickSize { get; set; } = 0.05m;
    public DateTime? Expiry { get; set; }
    public decimal? StrikePrice { get; set; }
    public OptionType? OptionType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class MarketCandle
{
    public long Id { get; set; }
    public string InstrumentKey { get; set; } = string.Empty;
    public CandleTimeframe Timeframe { get; set; }
    public DateTime CandleTimeUtc { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal? OpenInterest { get; set; }
    public bool IsFinalized { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
