namespace NiftyQuant.Domain.Enums;

public enum MarketRegimeType
{
    Transitional = 0,
    StrongBullish = 1,
    StrongBearish = 2,
    Sideways = 3,
    HighVolatility = 4
}

public enum SignalDirection
{
    Wait = 0,
    NoTrade = 1,
    BuyCe = 2,
    StrongBuyCe = 3,
    BuyPe = 4,
    StrongBuyPe = 5
}

public enum StrategyFamily
{
    None = 0,
    TrendFollowing = 1,
    Breakout = 2,
    Reversal = 3
}

public enum OptionType
{
    CE = 1,
    PE = 2
}

public enum OiBuildupType
{
    Neutral = 0,
    LongBuildup = 1,
    ShortBuildup = 2,
    ShortCovering = 3,
    LongUnwinding = 4
}

public enum OptionsSentiment
{
    ExtremelyBearish = 1,
    Bearish = 2,
    Neutral = 3,
    Bullish = 4,
    ExtremelyBullish = 5
}

public enum CandleTimeframe
{
    OneMinute = 1,
    FiveMinute = 5,
    FifteenMinute = 15
}

public enum PaperOrderStatus
{
    Pending = 0,
    Filled = 1,
    Cancelled = 2,
    Rejected = 3
}

public enum PaperOrderSide
{
    Buy = 1,
    Sell = 2
}

public enum PaperTradeStatus
{
    Open = 1,
    Closed = 2
}
