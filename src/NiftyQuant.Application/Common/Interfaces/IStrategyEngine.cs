using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IStrategyEngine
{
    event Func<StrategySignal, Task>? OnSignalGenerated;

    Task<StrategySignal> EvaluateStrategiesAsync(StrategyAnalysisContext context, CancellationToken ct = default);

    Task<StrategySignal?> GetLatestSignalAsync(string underlyingKey = "NSE_INDEX|Nifty 50", CancellationToken ct = default);

    Task<List<StrategySignal>> GetSignalHistoryAsync(string underlyingKey = "NSE_INDEX|Nifty 50", int limit = 50, CancellationToken ct = default);
}
