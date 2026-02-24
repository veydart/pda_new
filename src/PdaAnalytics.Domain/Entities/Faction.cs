namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Фракция (группировка). Данные из MariaDB `factions`.
/// </summary>
public class Faction
{
    /// <summary> Оригинальный ID фракции из MariaDB. </summary>
    public int SourceId { get; set; }
    
    /// <summary> Название фракции. </summary>
    public required string Name { get; set; }
    
    /// <summary> Color value (ARGB). </summary>
    public long Color { get; set; }
    
    /// <summary> Icon path/identifier. </summary>
    public string? Icon { get; set; }
    
    /// <summary> ID исходного инстанса. </summary>
    public required string SourceInstance { get; set; }
    
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    
    // ── Навигация ──
    
    public ICollection<FactionMembership> Members { get; set; } = [];
}
