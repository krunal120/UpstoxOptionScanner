using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Interfaces;

public interface ICandleAggregationEngine
{
    event Func<MarketCandle, Task>? OnCandleFinalized;

    Task ProcessTickAsync(MarketTick tick, CancellationToken ct = default);
    MarketCandle? GetCurrentCandle(string instrumentKey, CandleTimeframe timeframe);
    IReadOnlyList<MarketCandle> GetRecentCandles(string instrumentKey, CandleTimeframe timeframe, int count = 100);
}
