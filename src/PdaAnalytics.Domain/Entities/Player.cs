namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Игрок — агрегат данных по SteamID. Данные из MariaDB `players`.
/// </summary>
public class Player
{
    /// <summary> SteamID64 — первичный ключ. </summary>
    public required string SteamId { get; set; }
    
    /// <summary> Последний известный никнейм в игре. </summary>
    public string? Nickname { get; set; }
    
    /// <summary> Дата первой регистрации на сервере. </summary>
    public DateTime RegistrationDate { get; set; }
    
    /// <summary> Дата последнего входа на сервер. </summary>
    public DateTime LastLogonDate { get; set; }
    
    /// <summary> ID исходного инстанса (instance0..instance_4). </summary>
    public required string SourceInstance { get; set; }
    
    /// <summary> Дата последней синхронизации из MariaDB. </summary>
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    
    // ── Навигационные свойства ──
    
    public ICollection<PdaAccount> PdaAccounts { get; set; } = [];
    public ICollection<FactionMembership> FactionMemberships { get; set; } = [];
}
