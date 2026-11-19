using System.Text.Json.Serialization;

namespace NiftyQuant.Application.Common.Models.Upstox;

public class UpstoxResponse<T>
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<UpstoxError>? Errors { get; set; }
}

public class UpstoxError
{
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("propertyPath")]
    public string? PropertyPath { get; set; }
}

public class MarketQuoteDto
{
    [JsonPropertyName("instrument_token")]
    public string InstrumentToken { get; set; } = string.Empty;

    [JsonPropertyName("last_price")]
    public decimal LastPrice { get; set; }

    [JsonPropertyName("net_change")]
    public decimal? NetChange { get; set; }

    [JsonPropertyName("volume")]
    public long? Volume { get; set; }

    [JsonPropertyName("oi")]
    public decimal? OpenInterest { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("ohlc")]
    public OhlcDto? Ohlc { get; set; }

    [JsonPropertyName("depth")]
    public MarketDepthDto? Depth { get; set; }
}

public class OhlcDto
{
    [JsonPropertyName("open")]
    public decimal Open { get; set; }

    [JsonPropertyName("high")]
    public decimal High { get; set; }

    [JsonPropertyName("low")]
    public decimal Low { get; set; }

    [JsonPropertyName("close")]
    public decimal Close { get; set; }
}

public class MarketDepthDto
{
    [JsonPropertyName("buy")]
    public List<DepthItemDto>? Buy { get; set; }

    [JsonPropertyName("sell")]
    public List<DepthItemDto>? Sell { get; set; }
}

public class DepthItemDto
{
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("orders")]
    public int Orders { get; set; }
}

public class OptionContractDto
{
    [JsonPropertyName("instrument_key")]
    public string InstrumentKey { get; set; } = string.Empty;

    [JsonPropertyName("exchange_token")]
    public string ExchangeToken { get; set; } = string.Empty;

    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("expiry")]
    public string Expiry { get; set; } = string.Empty;

    [JsonPropertyName("strike_price")]
    public decimal StrikePrice { get; set; }

    [JsonPropertyName("instrument_type")]
    public string InstrumentType { get; set; } = string.Empty; // CE or PE

    [JsonPropertyName("lot_size")]
    public int LotSize { get; set; }

    [JsonPropertyName("underlying_key")]
    public string UnderlyingKey { get; set; } = string.Empty;

    [JsonPropertyName("underlying_symbol")]
    public string UnderlyingSymbol { get; set; } = string.Empty;

    [JsonPropertyName("weekly")]
    public bool Weekly { get; set; }
}

public class OptionChainItemDto
{
    [JsonPropertyName("expiry")]
    public string Expiry { get; set; } = string.Empty;

    [JsonPropertyName("pcr")]
    public decimal Pcr { get; set; }

    [JsonPropertyName("strike_price")]
    public decimal StrikePrice { get; set; }

    [JsonPropertyName("underlying_key")]
    public string UnderlyingKey { get; set; } = string.Empty;

    [JsonPropertyName("underlying_spot_price")]
    public decimal UnderlyingSpotPrice { get; set; }

    [JsonPropertyName("call_options")]
    public OptionDetailsDto? CallOptions { get; set; }

    [JsonPropertyName("put_options")]
    public OptionDetailsDto? PutOptions { get; set; }
}

public class OptionDetailsDto
{
    [JsonPropertyName("instrument_key")]
    public string InstrumentKey { get; set; } = string.Empty;

    [JsonPropertyName("market_data")]
    public OptionMarketDataDto? MarketData { get; set; }

    [JsonPropertyName("option_greeks")]
    public OptionGreeksDto? OptionGreeks { get; set; }
}

public class OptionMarketDataDto
{
    [JsonPropertyName("ltp")]
    public decimal Ltp { get; set; }

    [JsonPropertyName("volume")]
    public long Volume { get; set; }

    [JsonPropertyName("oi")]
    public decimal OpenInterest { get; set; }

    [JsonPropertyName("prev_oi")]
    public decimal PreviousOpenInterest { get; set; }

    [JsonPropertyName("bid_price")]
    public decimal BidPrice { get; set; }

    [JsonPropertyName("bid_qty")]
    public int BidQty { get; set; }

    [JsonPropertyName("ask_price")]
    public decimal AskPrice { get; set; }

    [JsonPropertyName("ask_qty")]
    public int AskQty { get; set; }

    [JsonPropertyName("close_price")]
    public decimal ClosePrice { get; set; }
}

public class OptionGreeksDto
{
    [JsonPropertyName("vega")]
    public decimal Vega { get; set; }

    [JsonPropertyName("theta")]
    public decimal Theta { get; set; }

    [JsonPropertyName("gamma")]
    public decimal Gamma { get; set; }

    [JsonPropertyName("delta")]
    public decimal Delta { get; set; }

    [JsonPropertyName("iv")]
    public decimal ImpliedVolatility { get; set; }

    [JsonPropertyName("pop")]
    public decimal? ProbabilityOfProfit { get; set; }
}

public class HistoricalCandlesDataDto
{
    [JsonPropertyName("candles")]
    public List<List<object>>? RawCandles { get; set; }
}

public class CandleDto
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal OpenInterest { get; set; }
}

public class InstrumentMasterDto
{
    public string InstrumentKey { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public decimal? StrikePrice { get; set; }
    public string? InstrumentType { get; set; }
    public DateTime? Expiry { get; set; }
    public int LotSize { get; set; }
    public decimal TickSize { get; set; }
}
