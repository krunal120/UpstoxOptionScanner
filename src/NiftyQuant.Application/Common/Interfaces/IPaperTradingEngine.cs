using NiftyQuant.Application.Common.Models.PaperTrading;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IPaperTradingEngine
{
    event Func<PaperOrder, Task>? OnOrderPlaced;
    event Func<PaperOrder, Task>? OnOrderExecuted;
    event Func<PaperPosition, Task>? OnPositionOpened;
    event Func<PaperPosition, Task>? OnPositionUpdated;
    event Func<PaperTrade, Task>? OnPositionClosed;

    Task<OrderExecutionResult> ExecuteSignalAsync(StrategySignal signal, bool simulateSlippage = true, CancellationToken ct = default);

    Task<ClosePositionResult> ClosePositionAsync(Guid positionId, string exitReason = "ManualClose", decimal? exitPrice = null, CancellationToken ct = default);

    Task<List<ClosePositionResult>> SquareOffAllPositionsAsync(string exitReason = "EndOfDaySquareoff", CancellationToken ct = default);

    Task UpdatePositionPricesAsync(string instrumentKey, decimal currentLtp, CancellationToken ct = default);

    Task<IReadOnlyList<PaperPosition>> GetOpenPositionsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PaperTrade>> GetTradeHistoryAsync(int limit = 50, CancellationToken ct = default);

    Task<IReadOnlyList<PaperOrder>> GetOrderHistoryAsync(int limit = 50, CancellationToken ct = default);

    Task<PaperPortfolioSummaryDto> GetPortfolioSummaryAsync(CancellationToken ct = default);
}
