using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Services.Indicators;
using NiftyQuant.Application.Services.Options;
using NiftyQuant.Application.Services.Strategy;

namespace NiftyQuant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITechnicalIndicatorService, TechnicalIndicatorService>();
        services.AddSingleton<IOptionsIntelligenceService, OptionsIntelligenceService>();

        // Strategy Evaluators & Regime Classifier
        services.AddSingleton<IMarketRegimeClassifier, MarketRegimeClassifier>();
        services.AddSingleton<IStrategyEvaluator, TrendFollowingStrategyEvaluator>();
        services.AddSingleton<IStrategyEvaluator, BreakoutStrategyEvaluator>();
        services.AddSingleton<IStrategyEvaluator, ReversalStrategyEvaluator>();

        return services;
    }
}
