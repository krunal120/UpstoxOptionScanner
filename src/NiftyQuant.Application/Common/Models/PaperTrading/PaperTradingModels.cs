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
    public PaperOrderSide Side { get; set; }
    public int Quantity { get; set; }
    public decimal OrderedPrice { get; set; }
    public decimal ExecutedPrice { get; set; }
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
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal GrossPnL { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal NetPnL { get; set; }
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
