using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Models.Backtesting;

public class BacktestRequest
{
    public string UnderlyingKey { get; set; } = "NSE_INDEX|Nifty 50";
    public DateTime FromDate { get; set; } = DateTime.UtcNow.AddDays(-14);
    public DateTime ToDate { get; set; } = DateTime.UtcNow;
    public decimal InitialCapital { get; set; } = 50000m;
    public decimal RiskPerTradePercent { get; set; } = 0.5m;
    public decimal MaxDailyLossPercent { get; set; } = 1.5m;
    public int MaxDailyTrades { get; set; } = 6;
    public int MaxConsecutiveLosses { get; set; } = 2;
    public decimal MinRewardRiskRatio { get; set; } = 2.0m;
    public bool SimulateSlippage { get; set; } = true;
    public decimal SlippagePercent { get; set; } = 0.2m;
    public bool EnableTrailingStop { get; set; } = true;
    public StrategyFamily? TargetStrategy { get; set; } // Null for all strategies
}

public class BacktestTradeDto
{
    public int TradeNumber { get; set; }
    public DateTime EntryTimeUtc { get; set; }
    public DateTime ExitTimeUtc { get; set; }
    public string InstrumentKey { get; set; } = string.Empty;
    public SignalDirection Direction { get; set; }
    public StrategyFamily StrategyFamily { get; set; }
    public MarketRegimeType MarketRegime { get; set; }
    public int Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal StopLossPrice { get; set; }
    public decimal TargetPrice { get; set; }
    public decimal GrossPnL { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal NetPnL { get; set; }
    public decimal ReturnPercent { get; set; }
    public string ExitReason { get; set; } = string.Empty; // TargetHit, StopLossHit, TrailingStopHit, EndOfDaySquareoff
    public decimal RunningCapital { get; set; }
}

public class EquityPointDto
{
    public DateTime TimestampUtc { get; set; }
    public decimal Capital { get; set; }
    public decimal DrawdownAmount { get; set; }
    public decimal DrawdownPercent { get; set; }
    public int TradeNumber { get; set; }
}

public class DailyPerformanceDto
{
    public DateTime Date { get; set; }
    public int TradesCount { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal GrossPnL { get; set; }
    public decimal NetPnL { get; set; }
    public decimal EndingCapital { get; set; }
    public bool IsDailyLossHit { get; set; }
}

public class StrategyBreakdownDto
{
    public StrategyFamily StrategyFamily { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public decimal WinRatePercent { get; set; }
    public decimal GrossPnL { get; set; }
    public decimal NetPnL { get; set; }
    public decimal ProfitFactor { get; set; }
}

public class RegimeBreakdownDto
{
    public MarketRegimeType Regime { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public decimal WinRatePercent { get; set; }
    public decimal NetPnL { get; set; }
}

public class BacktestResultDto
{
    public Guid RunId { get; set; } = Guid.NewGuid();
    public string UnderlyingKey { get; set; } = "NSE_INDEX|Nifty 50";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalTradingDays { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalCapital { get; set; }
    public decimal TotalGrossPnL { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal TotalNetPnL { get; set; }
    public decimal TotalReturnPercent { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRatePercent { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal AverageTradePnL { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal WinLossRatio { get; set; }
    public decimal MathematicalExpectancy { get; set; }
    public decimal MaxDrawdownAmount { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal SortinoRatio { get; set; }
    public int DailyLossLimitBreaches { get; set; }

    public List<BacktestTradeDto> Trades { get; set; } = new();
    public List<DailyPerformanceDto> DailyPerformance { get; set; } = new();
    public List<EquityPointDto> EquityCurve { get; set; } = new();
    public List<StrategyBreakdownDto> StrategyBreakdowns { get; set; } = new();
    public List<RegimeBreakdownDto> RegimeBreakdowns { get; set; } = new();
}
