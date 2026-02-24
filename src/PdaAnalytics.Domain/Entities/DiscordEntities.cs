namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Привязка Discord Webhook к конкретному глобальному чату.
/// Когда в чате ChatSourceId появляется новое сообщение — оно транслируется в WebhookUrl.
/// </summary>
public class ChatWebhookConfig
{
    public int Id { get; set; }

    /// <summary>
    /// Оригинальный ID чата из MariaDB (pda_chats.ID).
    /// Должен быть GLOBAL-типа для трансляции.
    /// </summary>
    public int ChatSourceId { get; set; }

    /// <summary>
    /// Имя инстанса (instance0, instance1, ...) — нужен для уникальности ChatSourceId.
    /// </summary>
    public required string SourceInstance { get; set; }

    /// <summary>
    /// Имя чата — денормализированное для удобства отображения в UI.
    /// </summary>
    public string? ChatName { get; set; }

    /// <summary> Discord Webhook URL. </summary>
    public required string WebhookUrl { get; set; }

    /// <summary> Включена ли трансляция. </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary> Дата создания правила. </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary> Дата последнего изменения. </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Правило упоминания фракции.
/// Если текст сообщения содержит один из алиасов (или @Слово из списка),
/// отправляем уведомление в WebhookUrl этой фракции.
/// </summary>
public class FactionMentionConfig
{
    public int Id { get; set; }

    /// <summary>
    /// Оригинальный ID фракции из MariaDB (factions.ID) — опционально,
    /// может быть null если правило не привязано к конкретной фракции.
    /// </summary>
    public int? FactionSourceId { get; set; }

    /// <summary> Имя инстанса (для FactionSourceId). </summary>
    public string? SourceInstance { get; set; }

    /// <summary>
    /// Отображаемое имя правила (например: "СВБД", "Медики", "ООН").
    /// Денормализировано для UI.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// JSON-массив алиасов-триггеров.
    /// Пример: ["SVBD", "СВБД", "@свбд", "bandits"]
    /// Сравнение регистронезависимое. Ищутся как цельные слова (word-boundary или @prefix).
    /// Хранится как TEXT (JSON-сериализованный List&lt;string&gt;).
    /// </summary>
    public required string AliasesJson { get; set; }

    /// <summary>
    /// ID роли в Discord для пинга при упоминании.
    /// Если указан — в сообщение добавляется &lt;@&amp;ROLE_ID&gt;.
    /// Пример: "1234567890123456789".
    /// </summary>
    public string? DiscordRoleId { get; set; }

    /// <summary> Discord Webhook URL для этой фракции. </summary>
    public required string WebhookUrl { get; set; }

    /// <summary> Включено ли правило. </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary> Дата создания. </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary> Дата последнего изменения. </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Событие для очереди Discord-диспетчера.
/// Содержит всё необходимое для формирования Discord-сообщения
/// без повторных запросов к БД.
/// </summary>
public class DiscordDispatchEvent
{
    /// <summary> Тип события: трансляция чата или упоминание фракции. </summary>
    public DiscordEventType EventType { get; set; }

    /// <summary> Целевой Webhook URL. </summary>
    public required string WebhookUrl { get; set; }

    /// <summary> Само сообщение (для формирования embed/text). </summary>
    public required MessageDenormalized Message { get; set; }

    /// <summary>
    /// Для упоминаний — имя фракции/правила, которое было упомянуто.
    /// Null для трансляций чатов.
    /// </summary>
    public string? MentionedFactionName { get; set; }

    /// <summary>
    /// ID Discord-роли для пинга. Null — без пинга.
    /// </summary>
    public string? DiscordRoleId { get; set; }
}

public enum DiscordEventType
{
    /// <summary> Полная трансляция сообщения из чата. </summary>
    ChatBroadcast,

    /// <summary> Упоминание фракции в любом чате. </summary>
    FactionMention,
}
