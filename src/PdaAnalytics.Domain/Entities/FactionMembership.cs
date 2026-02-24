namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Членство в фракции. Связывает Player (SteamID) и Faction.
/// Данные из MariaDB `faction_members`.
/// </summary>
public class FactionMembership
{
    /// <summary> Суррогатный PK в Postgres. </summary>
    public long Id { get; set; }
    
    /// <summary> ID фракции (SourceId из Faction). </summary>
    public int FactionSourceId { get; set; }
    
    /// <summary> Rank ID. </summary>
    public int RankId { get; set; }
    
    /// <summary> SteamID участника (MEMBER_ID). </summary>
    public required string MemberSteamId { get; set; }
    
    /// <summary> ID исходного инстанса. </summary>
    public required string SourceInstance { get; set; }
    
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    
    // ── Навигация ──
    
    public Faction? Faction { get; set; }
    public Player? Player { get; set; }
}
