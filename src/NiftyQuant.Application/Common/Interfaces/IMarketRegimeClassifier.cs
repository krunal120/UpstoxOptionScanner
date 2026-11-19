using NiftyQuant.Application.Common.Models.Strategy;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IMarketRegimeClassifier
{
    MarketRegimeResult ClassifyRegime(StrategyAnalysisContext context);
}
