using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PdaAnalytics.Data;

/// <summary>
/// Фабрика для design-time (dotnet ef migrations add ...).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AnalyticsDbContext>
{
    public AnalyticsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AnalyticsDbContext>();
        
        // Строка подключения по умолчанию для миграций
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=pda_analytics;Username=postgres;Password=root",
            npgsql => npgsql.MigrationsAssembly("PdaAnalytics.Data"));

        return new AnalyticsDbContext(optionsBuilder.Options);
    }
}
