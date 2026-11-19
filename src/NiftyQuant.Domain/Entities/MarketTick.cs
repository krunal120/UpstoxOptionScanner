namespace NiftyQuant.Domain.Entities;

public class MarketTick
{
    public string InstrumentKey { get; set; } = string.Empty;
    public decimal Ltp { get; set; }
    public long? LastTradedQuantity { get; set; }
    public DateTime? LastTradedTime { get; set; }
    public decimal? ClosePrice { get; set; }
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public decimal? Close { get; set; }
    public long Volume { get; set; }
    public decimal OpenInterest { get; set; }
    public decimal BidPrice { get; set; }
    public int BidQuantity { get; set; }
    public decimal AskPrice { get; set; }
    public int AskQuantity { get; set; }
    public decimal? Delta { get; set; }
    public decimal? Theta { get; set; }
    public decimal? Gamma { get; set; }
    public decimal? Vega { get; set; }
    public decimal? ImpliedVolatility { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public bool IsIndex { get; set; }
}
