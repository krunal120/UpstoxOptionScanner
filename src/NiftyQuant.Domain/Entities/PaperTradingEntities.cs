using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Domain.Entities;

public class PaperAccount
{
    public int Id { get; set; }
    public string AccountName { get; set; } = "Default Paper Account";
    public decimal InitialCapital { get; set; } = 50000.00m;
    public decimal AvailableBalance { get; set; } = 50000.00m;
    public decimal UtilizedMargin { get; set; } = 0.00m;
    public decimal RealizedPnL { get; set; } = 0.00m;
    public decimal UnrealizedPnL { get; set; } = 0.00m;
    public decimal DailyLossLimit { get; set; } = 750.00m; // 1.5% of 50,000
    public decimal MaxRiskPerTrade { get; set; } = 250.00m; // 0.5% of 50,000
    public bool IsTradingLockedForDay { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class PaperOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SignalId { get; set; }
    public string InstrumentKey { get; set; } = string.Empty;
    public string? TradingSymbol { get; set; } = string.Empty;
    public PaperOrderSide Side { get; set; }
    public OptionType OptionType { get; set; } = OptionType.CE;
    public decimal StrikePrice { get; set; }
    public int Quantity { get; set; }
    public decimal OrderedPrice { get; set; }
    public decimal ExecutedPrice { get; set; }
    public decimal Slippage { get; set; }
    public decimal EstimatedCharges { get; set; }
    public PaperOrderStatus Status { get; set; }
    public string RejectReason { get; set; } = string.Empty;
    public DateTime OrderTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedTimeUtc { get; set; }
}

public class PaperPosition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InstrumentKey { get; set; } = string.Empty;
    public string? TradingSymbol { get; set; } = string.Empty;
    public OptionType OptionType { get; set; }
    public decimal StrikePrice { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public decimal AverageBuyPrice { get; set; }
    public decimal CurrentLtp { get; set; }
    public decimal StopLossPrice { get; set; }
    public decimal TargetPrice { get; set; }
    public decimal TrailingStopLossPrice { get; set; }
    public decimal MaxLtpReached { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public bool IsOpen { get; set; } = true;
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
}

public class PaperTrade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SignalId { get; set; }
    public string InstrumentKey { get; set; } = string.Empty;
    public string? TradingSymbol { get; set; } = string.Empty;
    public OptionType OptionType { get; set; } = OptionType.CE;
    public decimal StrikePrice { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal SlippagePaid { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal GrossPnL { get; set; }
    public decimal NetPnL { get; set; }
    public decimal ReturnPercent { get; set; }
    public double DurationMinutes { get; set; }
    public DateTime EntryTimeUtc { get; set; }
    public DateTime ExitTimeUtc { get; set; }
    public string ExitReason { get; set; } = string.Empty; // TargetHit, StopLossHit, EndOfDaySquareoff, RiskVeto
    public PaperTradeStatus Status { get; set; } = PaperTradeStatus.Closed;
}
