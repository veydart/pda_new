namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Денормализированное сообщение — главная аналитическая таблица.
/// Содержит все данные для мгновенного поиска: SteamID отправителя и получателя
/// уже предрассчитаны из связки pda_chat_messages → pda_chat_participants → pda_accounts.
/// </summary>
public class MessageDenormalized
{
    /// <summary> Суррогатный PK (auto-increment в Postgres). </summary>
    public long Id { get; set; }
    
    /// <summary> Оригинальный ID сообщения из MariaDB pda_chat_messages.ID. </summary>
    public int SourceMessageId { get; set; }
    
    /// <summary> Оригинальный ID чата из MariaDB. </summary>
    public int SourceChatId { get; set; }
    
    /// <summary> Тип чата (PRIVATE / GLOBAL). </summary>
    public ChatType ChatType { get; set; }
    
    /// <summary> Название чата (для GLOBAL) или null. </summary>
    public string? ChatName { get; set; }
    
    // ── Данные об отправителе ──
    
    /// <summary> PDA Account ID отправителя. </summary>
    public int SenderAccountId { get; set; }
    
    /// <summary> Логин PDA отправителя. </summary>
    public string? SenderLogin { get; set; }
    
    /// <summary> SteamID64 отправителя. </summary>
    public string? SenderSteamId { get; set; }
    
    /// <summary> Никнейм игрока-отправителя. </summary>
    public string? SenderNickname { get; set; }
    
    // ── Данные о получателе (для PRIVATE чатов) ──
    
    /// <summary> PDA Account ID получателя. Null для GLOBAL. </summary>
    public int? ReceiverAccountId { get; set; }
    
    /// <summary> Логин PDA получателя. </summary>
    public string? ReceiverLogin { get; set; }
    
    /// <summary> SteamID64 получателя. </summary>
    public string? ReceiverSteamId { get; set; }
    
    /// <summary> Никнейм игрока-получателя. </summary>
    public string? ReceiverNickname { get; set; }
    
    // ── Содержимое ──
    
    /// <summary> Текст сообщения. </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary> JSON вложений (метки на карте, изображения и т.д.). </summary>
    public string? Attachments { get; set; }
    
    /// <summary> Дата отправки сообщения. </summary>
    public DateTime SentAt { get; set; }
    
    /// <summary> ID исходного инстанса. </summary>
    public required string SourceInstance { get; set; }
    
    /// <summary> Когда было синхронизировано. </summary>
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}
