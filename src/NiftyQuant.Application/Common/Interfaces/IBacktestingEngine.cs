using NiftyQuant.Application.Common.Models.Backtesting;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IBacktestingEngine
{
    Task<BacktestResultDto> RunBacktestAsync(BacktestRequest request, CancellationToken ct = default);

    Task<BacktestResultDto> RunBacktestOnCandlesAsync(
        IReadOnlyList<MarketCandle> candles1m,
        IReadOnlyList<MarketCandle> candles5m,
        IReadOnlyList<MarketCandle> candles15m,
        IReadOnlyList<MarketCandle> dailyCandles,
        BacktestRequest request,
        CancellationToken ct = default);
}
