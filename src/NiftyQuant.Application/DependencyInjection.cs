using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Services.Indicators;

namespace NiftyQuant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITechnicalIndicatorService, TechnicalIndicatorService>();
        return services;
    }
}
