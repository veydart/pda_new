namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// PDA-аккаунт. Один игрок (SteamID) может иметь несколько PDA-аккаунтов (мультиаккаунты).
/// Данные из MariaDB `pda_accounts`.
/// </summary>
public class PdaAccount
{
    /// <summary> Оригинальный ID из MariaDB (PK). </summary>
    public int SourceId { get; set; }
    
    /// <summary> Логин (виден другим игрокам в PDA). </summary>
    public required string Login { get; set; }
    
    /// <summary> SteamID владельца. </summary>
    public required string SteamId { get; set; }
    
    /// <summary> Последняя активность PDA-аккаунта. </summary>
    public DateTime LastActivity { get; set; }
    
    /// <summary> ID исходного инстанса. </summary>
    public required string SourceInstance { get; set; }
    
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    
    // ── Навигация ──
    
    public Player? Player { get; set; }
}
