namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Хранит состояние последней синхронизации для каждой таблицы/инстанса.
/// Позволяет ETL-воркеру знать, откуда продолжить инкрементальную загрузку.
/// </summary>
public class SyncState
{
    /// <summary> Суррогатный PK. </summary>
    public int Id { get; set; }
    
    /// <summary> Имя исходного инстанса (instance0, instance1, ...). </summary>
    public required string InstanceName { get; set; }
    
    /// <summary> Имя исходной таблицы (pda_chat_messages, players, ...). </summary>
    public required string TableName { get; set; }
    
    /// <summary> Последний синхронизированный ID (auto-increment). Для таблиц с int PK. </summary>
    public long LastSyncedId { get; set; }
    
    /// <summary> Последняя синхронизированная дата. Для таблиц без auto-increment PK. </summary>
    public DateTime? LastSyncedAt { get; set; }
    
    /// <summary> Когда был выполнен последний цикл синхронизации. </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
