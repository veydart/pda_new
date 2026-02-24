namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Участник чата. Связывает Chat и PdaAccount. 
/// Из MariaDB `pda_chat_participants`.
/// </summary>
public class ChatParticipant
{
    /// <summary> Суррогатный PK (в Postgres). </summary>
    public long Id { get; set; }
    
    /// <summary> ID чата (SourceId из Chat). </summary>
    public int ChatSourceId { get; set; }
    
    /// <summary> ID PDA-аккаунта участника. </summary>
    public int AccountSourceId { get; set; }
    
    /// <summary> ID PDA-аккаунта контакта (для приватных чатов). </summary>
    public int ContactAccountSourceId { get; set; }
    
    /// <summary> ID исходного инстанса. </summary>
    public required string SourceInstance { get; set; }
    
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    
    // ── Навигация ──
    
    public Chat? Chat { get; set; }
    public PdaAccount? Account { get; set; }
    public PdaAccount? ContactAccount { get; set; }
}
