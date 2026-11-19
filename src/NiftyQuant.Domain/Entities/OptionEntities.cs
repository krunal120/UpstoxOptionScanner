using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Domain.Entities;

public class OptionContract
{
    public string InstrumentKey { get; set; } = string.Empty;
    public string UnderlyingKey { get; set; } = "NSE_INDEX|Nifty 50";
    public decimal StrikePrice { get; set; }
    public OptionType OptionType { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int LotSize { get; set; } = 50;
    public bool IsActive { get; set; } = true;
}

public class OptionChainSnapshot
{
    public long Id { get; set; }
    public string UnderlyingKey { get; set; } = "NSE_INDEX|Nifty 50";
    public decimal UnderlyingPrice { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime SnapshotTimeUtc { get; set; }
    public decimal TotalCallOi { get; set; }
    public decimal TotalPutOi { get; set; }
    public decimal Pcr { get; set; }
    public decimal? MaxPainStrike { get; set; }
    public decimal HighestCallOiStrike { get; set; }
    public decimal HighestPutOiStrike { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public List<OptionQuote> Quotes { get; set; } = new();
}

public class OptionQuote
{
    public long Id { get; set; }
    public long? SnapshotId { get; set; }
    public string InstrumentKey { get; set; } = string.Empty;
    public decimal StrikePrice { get; set; }
    public OptionType OptionType { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal Ltp { get; set; }
    public decimal BidPrice { get; set; }
    public decimal AskPrice { get; set; }
    public long Volume { get; set; }
    public decimal OpenInterest { get; set; }
    public decimal PreviousOpenInterest { get; set; }
    public decimal IntradayOiChange { get; set; }
    public decimal IntradayOiChangePercent { get; set; }
    public decimal? ImpliedVolatility { get; set; }
    public decimal? Delta { get; set; }
    public decimal? Gamma { get; set; }
    public decimal? Theta { get; set; }
    public decimal? Vega { get; set; }
    public DateTime SnapshotTimeUtc { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public OptionChainSnapshot? Snapshot { get; set; }
}
