using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Domain.Entities;

public class StrategySignal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SignalTimeUtc { get; set; } = DateTime.UtcNow;
    public string UnderlyingKey { get; set; } = "NSE_INDEX|Nifty 50";
    public decimal UnderlyingPrice { get; set; }
    public SignalDirection Direction { get; set; }
    public StrategyFamily StrategyFamily { get; set; }
    public MarketRegimeType MarketRegime { get; set; }
    public decimal BullishScore { get; set; }
    public decimal BearishScore { get; set; }
    public decimal Confidence { get; set; }
    public string SelectedInstrumentKey { get; set; } = string.Empty;
    public decimal SelectedStrike { get; set; }
    public OptionType SelectedOptionType { get; set; }
    public DateTime SelectedExpiry { get; set; }
    public decimal EntryPremium { get; set; }
    public decimal StopLossPremium { get; set; }
    public decimal Target1Premium { get; set; }
    public decimal Target2Premium { get; set; }
    public int RecommendedQuantity { get; set; }
    public int LotSize { get; set; } = 50;
    public decimal RiskAmount { get; set; }
    public decimal RewardToRiskRatio { get; set; }
    public string ConfigurationVersion { get; set; } = "1.0";
    public bool IsExecuted { get; set; } = false;
    public List<SignalReason> Reasons { get; set; } = new();
}

public class SignalReason
{
    public long Id { get; set; }
    public Guid SignalId { get; set; }
    public string Category { get; set; } = string.Empty; // Trend, Momentum, PriceAction, VolumeBreadth, OptionChain, Greeks
    public decimal ScoreContribution { get; set; }
    public string Description { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore]
    public StrategySignal? Signal { get; set; }
}
