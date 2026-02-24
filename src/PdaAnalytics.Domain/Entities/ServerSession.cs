namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Серверная сессия. Каждая смена MariaDB-подключения создаёт новую сессию.
/// Старые данные архивируются и привязываются к своей сессии.
/// </summary>
public class ServerSession
{
    public int Id { get; set; }
    
    /// <summary> Название сессии (например: "Сезон 3", "Main Server"). </summary>
    public required string Name { get; set; }
    
    /// <summary> Описание/комментарий. </summary>
    public string? Description { get; set; }
    
    /// <summary> Хост MariaDB, к которому подключались. </summary>
    public required string MariaDbHost { get; set; }
    
    /// <summary> Логин MariaDB. </summary>
    public required string MariaDbUser { get; set; }
    
    /// <summary> JSON-массив имён инстансов (databases). </summary>
    public required string InstanceNames { get; set; }
    
    /// <summary> Является ли эта сессия текущей (активной). Только одна может быть активной. </summary>
    public bool IsActive { get; set; }
    
    /// <summary> Дата создания сессии. </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary> Дата архивации (null = ещё не архивирована). </summary>
    public DateTime? ArchivedAt { get; set; }
    
    /// <summary> Общее кол-во сообщений на момент архивации. </summary>
    public int? ArchivedMessageCount { get; set; }
    
    /// <summary> Общее кол-во игроков на момент архивации. </summary>
    public int? ArchivedPlayerCount { get; set; }
}

/// <summary>
/// Системные настройки (key-value хранилище в PostgreSQL).
/// Используется для хранения текущего подключения MariaDB.
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }
    
    /// <summary> Ключ настройки. </summary>
    public required string Key { get; set; }
    
    /// <summary> Значение настройки. </summary>
    public required string Value { get; set; }
    
    /// <summary> Дата последнего изменения. </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Ключи системных настроек.
/// </summary>
public static class SettingKeys
{
    /// <summary> Хост MariaDB (IP:Port). </summary>
    public const string MariaDbHost = "mariadb.host";
    
    /// <summary> Логин MariaDB. </summary>
    public const string MariaDbUser = "mariadb.user";
    
    /// <summary> Пароль MariaDB. </summary>
    public const string MariaDbPassword = "mariadb.password";
    
    /// <summary> JSON-массив имён инстансов, например: ["instance0","instance1"]. </summary>
    public const string MariaDbInstances = "mariadb.instances";
    
    /// <summary> ID текущей активной сессии. </summary>
    public const string ActiveSessionId = "active_session_id";
    
    /// <summary> Интервал синхронизации (секунды). </summary>
    public const string SyncIntervalSeconds = "sync.interval_seconds";
    
    /// <summary> Размер батча сообщений. </summary>
    public const string SyncMessageBatchSize = "sync.message_batch_size";

    /// <summary> Steam Web API Key (https://steamcommunity.com/dev/apikey). </summary>
    public const string SteamApiKey = "steam.api_key";
}
