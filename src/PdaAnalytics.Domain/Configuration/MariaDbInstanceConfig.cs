namespace PdaAnalytics.Domain.Configuration;

/// <summary>
/// Конфигурация одного инстанса MariaDB.
/// </summary>
public class MariaDbInstanceConfig
{
    public required string Name { get; set; }
    public required string ConnectionString { get; set; }
}
