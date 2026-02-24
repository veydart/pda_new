namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Чат (PRIVATE или GLOBAL). Данные из MariaDB `pda_chats`.
/// </summary>
public class Chat
{
    /// <summary> Оригинальный ID чата из MariaDB. </summary>
    public int SourceId { get; set; }
    
    /// <summary> Название чата (для глобальных или group-чатов). </summary>
    public string? Name { get; set; }
    
    /// <summary> Тип: PRIVATE / GLOBAL. </summary>
    public ChatType Type { get; set; }
    
    /// <summary> ID исходного инстанса. </summary>
    public required string SourceInstance { get; set; }
    
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    
    // ── Навигация ──
    
    public ICollection<ChatParticipant> Participants { get; set; } = [];
}

public enum ChatType
{
    Global = 0,
    Private = 1
}
