using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Models.Risk;

public class PreTradeRiskValidationResult
{
    public bool IsApproved { get; set; }
    public List<string> VetoReasons { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int AllocatedLots { get; set; }
    public int AllocatedQuantity { get; set; }
    public int LotSize { get; set; } = 65;
    public decimal RequiredMargin { get; set; }
    public decimal ExpectedRiskAmount { get; set; }
    public decimal MaxLossAllowed { get; set; }
    public decimal RewardToRiskRatio { get; set; }
    public string ExecutionMode { get; set; } = "SimulatedPaperTradingOnly";
    public DateTime ValidatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DailyRiskStatusDto
{
    public DateTime Date { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal InitialCapital { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal UtilizedMargin { get; set; }
    public decimal RealizedPnLToday { get; set; }
    public decimal UnrealizedPnLToday { get; set; }
    public decimal TotalPnLToday { get; set; }
    public decimal DailyLossLimit { get; set; }
    public decimal DailyLossRemaining { get; set; }
    public int TradesCountToday { get; set; }
    public int MaxDailyTradesLimit { get; set; }
    public int ConsecutiveLossesToday { get; set; }
    public int MaxConsecutiveLossesLimit { get; set; }
    public int ActivePositionsCount { get; set; }
    public int MaxConcurrentPositionsLimit { get; set; }
    public bool IsCircuitBreakerTripped { get; set; }
    public string CircuitBreakerReason { get; set; } = string.Empty;
    public bool CanOpenNewPositions { get; set; }
}

public class PositionSizingResult
{
    public int AllocatedLots { get; set; }
    public int AllocatedQuantity { get; set; }
    public decimal RiskPerShare { get; set; }
    public decimal TotalRiskAmount { get; set; }
    public decimal TotalCost { get; set; }
    public bool IsAffordable { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
}
