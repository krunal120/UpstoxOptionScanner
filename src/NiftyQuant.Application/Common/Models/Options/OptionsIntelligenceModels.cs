using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Models.Options;

public class StrikeAnalysisDto
{
    public decimal StrikePrice { get; set; }
    public bool IsAtm { get; set; }
    public bool IsMajorCallResistance { get; set; }
    public bool IsMajorPutSupport { get; set; }

    public OptionQuote? CallQuote { get; set; }
    public OptionQuote? PutQuote { get; set; }

    public OiBuildupType CallBuildup { get; set; }
    public OiBuildupType PutBuildup { get; set; }

    public decimal CallOi { get; set; }
    public decimal PutOi { get; set; }
    public decimal CallOiChange { get; set; }
    public decimal PutOiChange { get; set; }
    public decimal NetOiChange { get; set; } // PutOiChange - CallOiChange

    public decimal? CallDelta { get; set; }
    public decimal? PutDelta { get; set; }
    public decimal? CallIv { get; set; }
    public decimal? PutIv { get; set; }

    public long CallVolume { get; set; }
    public long PutVolume { get; set; }

    public decimal CallBidAskSpreadPercent { get; set; }
    public decimal PutBidAskSpreadPercent { get; set; }
}

public class PcrAnalysisDto
{
    public decimal PcrOi { get; set; }
    public decimal PcrVolume { get; set; }
    public decimal? PreviousPcrOi { get; set; }
    public decimal? PcrChange { get; set; }
    public string PcrTrend { get; set; } = "Flat"; // Rising, Falling, Flat
    public string SentimentZone { get; set; } = "Neutral"; // Overbought, Bullish, Neutral, Bearish, Oversold
}

public class SupportResistanceLevelsDto
{
    public decimal SpotPrice { get; set; }
    public decimal MaxPainStrike { get; set; }
    public decimal MajorCallResistance { get; set; }
    public decimal MajorPutSupport { get; set; }
    public decimal? ImmediateCallResistance { get; set; }
    public decimal? ImmediatePutSupport { get; set; }
    public decimal ExpectedTradingRangeLow { get; set; }
    public decimal ExpectedTradingRangeHigh { get; set; }
}

public class SelectedOptionContractDto
{
    public string InstrumentKey { get; set; } = string.Empty;
    public OptionType OptionType { get; set; }
    public decimal StrikePrice { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal Ltp { get; set; }
    public decimal BidPrice { get; set; }
    public decimal AskPrice { get; set; }
    public decimal SpreadPercent { get; set; }
    public long Volume { get; set; }
    public decimal OpenInterest { get; set; }
    public decimal? Delta { get; set; }
    public decimal? ImpliedVolatility { get; set; }
    public decimal? Theta { get; set; }
    public decimal LiquidityScore { get; set; } // 0 - 100
    public bool IsRecommended { get; set; }
    public string SelectionReason { get; set; } = string.Empty;
}

public class OptionsIntelligenceReportDto
{
    public string UnderlyingKey { get; set; } = "NSE_INDEX|Nifty 50";
    public decimal UnderlyingPrice { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime SnapshotTimeUtc { get; set; }
    public int TotalStrikesAnalyzed { get; set; }

    public PcrAnalysisDto PcrAnalysis { get; set; } = new();
    public SupportResistanceLevelsDto KeyLevels { get; set; } = new();

    public int OptionsSentimentScore { get; set; } // -100 (Max Bearish) to +100 (Max Bullish)
    public OptionsSentiment OverallSentiment { get; set; }
    public string SentimentSummary { get; set; } = string.Empty;

    public SelectedOptionContractDto? RecommendedCe { get; set; }
    public SelectedOptionContractDto? RecommendedPe { get; set; }

    public List<StrikeAnalysisDto> Strikes { get; set; } = new();
}
