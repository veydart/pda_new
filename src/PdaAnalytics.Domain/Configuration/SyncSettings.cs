namespace PdaAnalytics.Domain.Configuration;

/// <summary>
/// Настройки синхронизации ETL.
/// </summary>
public class SyncSettings
{
    /// <summary> Интервал синхронизации в секундах. </summary>
    public int IntervalSeconds { get; set; } = 15;
    
    /// <summary> Максимальное количество сообщений за один batch. </summary>
    public int MessageBatchSize { get; set; } = 500;
}
