using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Infrastructure.Persistence;

namespace NiftyQuant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind Options
        services.Configure<UpstoxOptions>(configuration.GetSection(UpstoxOptions.SectionName));
        services.Configure<RiskOptions>(configuration.GetSection(RiskOptions.SectionName));

        // Configure SQL Server DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=.\\SQLSERVER2022_2;Database=NiftyQuantDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

        services.AddDbContext<NiftyQuantDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(NiftyQuantDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        });

        return services;
    }
}
