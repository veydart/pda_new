using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PdaAnalytics.Data;

public static class DataServiceExtensions
{
    /// <summary>
    /// Регистрирует AnalyticsDbContext с PostgreSQL.
    /// </summary>
    public static IServiceCollection AddAnalyticsData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AnalyticsDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql =>
                {
                    npgsql.MigrationsAssembly("PdaAnalytics.Data");
                    npgsql.CommandTimeout(60);
                });
        });

        return services;
    }
}
