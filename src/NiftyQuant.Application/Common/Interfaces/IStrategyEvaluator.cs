using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IStrategyEvaluator
{
    StrategyFamily Family { get; }
    StrategyEvaluationResult Evaluate(StrategyAnalysisContext context, MarketRegimeResult regime);
}
