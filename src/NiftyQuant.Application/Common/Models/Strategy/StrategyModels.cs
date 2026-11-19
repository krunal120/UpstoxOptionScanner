using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Models.Strategy;

public class MarketRegimeResult
{
    public MarketRegimeType Regime { get; set; } = MarketRegimeType.Transitional;
    public decimal Confidence { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal AdxValue { get; set; }
    public decimal IndiaVix { get; set; }
    public bool TrendAllowedBullish { get; set; }
    public bool TrendAllowedBearish { get; set; }
    public bool ReversalAllowed { get; set; }
}

public class StrategyEvaluationResult
{
    public StrategyFamily StrategyFamily { get; set; }
    public bool HasSignal { get; set; }
    public SignalDirection Direction { get; set; } = SignalDirection.Wait;
    public decimal BullishScore { get; set; }
    public decimal BearishScore { get; set; }
    public decimal Confidence { get; set; }
    public decimal StopLossUnderlyingDistance { get; set; }
    public decimal Target1UnderlyingDistance { get; set; }
    public decimal Target2UnderlyingDistance { get; set; }
    public List<SignalReasonDto> Reasons { get; set; } = new();
}

public class SignalReasonDto
{
    public string Category { get; set; } = string.Empty; // Trend, Momentum, PriceAction, VolumeBreadth, OptionChain, Greeks
    public decimal ScoreContribution { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class StrategyAnalysisContext
{
    public string UnderlyingKey { get; set; } = "NSE_INDEX|Nifty 50";
    public decimal CurrentPrice { get; set; }
    public decimal? IndiaVix { get; set; }
    public MultiTimeframeIndicatorState MultiTimeframeState { get; set; } = new();
    public OptionsIntelligenceReportDto OptionsReport { get; set; } = new();
    public decimal PreviousDayHigh { get; set; }
    public decimal PreviousDayLow { get; set; }
    public decimal PreviousDayClose { get; set; }
    public decimal IntradayHigh { get; set; }
    public decimal IntradayLow { get; set; }
    public DateTime EvaluationTimeUtc { get; set; } = DateTime.UtcNow;
}
