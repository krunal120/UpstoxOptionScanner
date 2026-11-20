using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Models.PaperTrading;

public class ExecuteSignalRequest
{
    public Guid? SignalId { get; set; }
    public string UnderlyingKey { get; set; } = "NSE_INDEX|Nifty 50";
    public StrategySignal? StrategySignal { get; set; }
    public bool SimulateSlippage { get; set; } = true;
}

public class OrderExecutionResult
{
    public bool IsSuccess { get; set; }
    public Guid OrderId { get; set; }
    public Guid? PositionId { get; set; }
    public string InstrumentKey { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public PaperOrderSide Side { get; set; }
    public OptionType OptionType { get; set; } = OptionType.CE;
    public decimal StrikePrice { get; set; }
    public int Quantity { get; set; }
    public decimal OrderedPrice { get; set; }
    public decimal ExecutedPrice { get; set; }
    public decimal StopLossPrice { get; set; }
    public decimal TargetPrice { get; set; }
    public decimal Slippage { get; set; }
    public decimal Charges { get; set; }
    public PaperOrderStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ClosePositionRequest
{
    public Guid PositionId { get; set; }
    public string ExitReason { get; set; } = "ManualClose"; // TargetHit, StopLossHit, TrailingStopHit, EndOfDaySquareoff, ManualClose
    public decimal? ExitPrice { get; set; }
}

public class ClosePositionResult
{
    public bool IsSuccess { get; set; }
    public Guid TradeId { get; set; }
    public Guid PositionId { get; set; }
    public string InstrumentKey { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal GrossPnL { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal NetPnL { get; set; }
    public decimal ReturnPercent { get; set; }
    public double DurationMinutes { get; set; }
    public DateTime ExitTimeUtc { get; set; } = DateTime.UtcNow;
    public string ExitReason { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class PaperPortfolioSummaryDto
{
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRatePercent { get; set; }
    public decimal TotalGrossPnL { get; set; }
    public decimal TotalChargesPaid { get; set; }
    public decimal TotalNetPnL { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal AverageProfitPerTrade { get; set; }
    public decimal AverageLossPerTrade { get; set; }
    public decimal Expectancy { get; set; }
    public decimal MaxDrawdownAmount { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal CurrentCapital { get; set; }
    public decimal AvailableBalance { get; set; }
    public int ActiveOpenPositionsCount { get; set; }
    public decimal ActiveUnrealizedPnL { get; set; }
}

public class AutonomousTradingStatusDto
{
    public bool IsEnabled { get; set; } = true;
    public string ExecutionMode { get; set; } = "Paper"; // "Paper" or "Live"
    public decimal MinConfidenceThreshold { get; set; } = 65.0m;
    public bool IsMarketHoursOnly { get; set; } = false;
    public DateTime? LastExecutionTime { get; set; }
    public int TotalOrdersPunchedToday { get; set; }
    public int TotalOrdersExitedToday { get; set; }
    public int ActivePositionsCount { get; set; }
    public string StatusMessage { get; set; } = "Autonomous Trader Running";
}

public class ToggleAutoTradingRequest
{
    public bool? Enabled { get; set; }
    public decimal? MinConfidence { get; set; }
    public bool? MarketHoursOnly { get; set; }
}

public class ForceSignalExecutionRequest
{
    public string OptionType { get; set; } = "CE"; // "CE" or "PE"
    public decimal? StrikePrice { get; set; }
    public decimal? Premium { get; set; }
    public decimal Confidence { get; set; } = 75.0m;
}
