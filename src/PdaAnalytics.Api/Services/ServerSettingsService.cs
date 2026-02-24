using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Configuration;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Api.Services;

/// <summary>
/// DTO для подключения к MariaDB (хранится в system_settings).
/// </summary>
public class MariaDbConnectionInfo
{
    public string Host { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public List<string> InstanceNames { get; set; } = [];

    /// <summary>
    /// Генерирует connection strings для каждого инстанса.
    /// </summary>
    public List<MariaDbInstanceConfig> ToInstanceConfigs()
    {
        return InstanceNames.Select(name => new MariaDbInstanceConfig
        {
            Name = name,
            ConnectionString = $"Server={Host};Database={name};User={User};Password={Password};SslMode=None;"
        }).ToList();
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) 
                                && !string.IsNullOrWhiteSpace(User)
                                && InstanceNames.Count > 0;
}

/// <summary>
/// Singleton-сервис с кэшем настроек MariaDB.
/// ETL-воркер читает настройки отсюда перед каждым циклом.
/// Инвалидируется при смене подключения через Admin UI.
/// </summary>
public class ServerSettingsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServerSettingsService> _logger;

    private MariaDbConnectionInfo? _cachedConnection;
    private int? _cachedActiveSessionId;
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Версия настроек. Инкрементируется при каждом изменении.
    /// ETL-воркер может сравнивать, чтобы понять, нужно ли перечитать.
    /// </summary>
    public long SettingsVersion { get; private set; }

    public ServerSettingsService(IServiceScopeFactory scopeFactory, ILogger<ServerSettingsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Получает текущие настройки подключения (из кэша или БД).
    /// </summary>
    public async Task<MariaDbConnectionInfo> GetConnectionInfoAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // Кэш актуален 30 сек
            if (_cachedConnection != null && DateTime.UtcNow - _lastCacheUpdate < TimeSpan.FromSeconds(30))
                return _cachedConnection;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

            var settings = await db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value);

            _cachedConnection = new MariaDbConnectionInfo
            {
                Host = settings.GetValueOrDefault(SettingKeys.MariaDbHost, ""),
                User = settings.GetValueOrDefault(SettingKeys.MariaDbUser, ""),
                Password = settings.GetValueOrDefault(SettingKeys.MariaDbPassword, ""),
                InstanceNames = settings.TryGetValue(SettingKeys.MariaDbInstances, out var instances)
                    ? JsonSerializer.Deserialize<List<string>>(instances) ?? []
                    : []
            };

            _cachedActiveSessionId = settings.TryGetValue(SettingKeys.ActiveSessionId, out var sesId)
                ? int.TryParse(sesId, out var id) ? id : null
                : null;

            _lastCacheUpdate = DateTime.UtcNow;
            return _cachedConnection;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// ID текущей активной сессии.
    /// </summary>
    public async Task<int?> GetActiveSessionIdAsync()
    {
        await GetConnectionInfoAsync(); // обновит кэш
        return _cachedActiveSessionId;
    }

    /// <summary>
    /// Сохраняет новое подключение MariaDB + создаёт сессию + архивирует старые данные.
    /// Вызывается из SettingsController.
    /// </summary>
    public async Task<ServerSession> SaveConnectionAndArchiveAsync(
        string host, string user, string password,
        List<string> instanceNames, string sessionName, string? description)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // ═══ 1. Архивация старых данных ═══
            var oldSession = await db.ServerSessions.FirstOrDefaultAsync(s => s.IsActive);
            if (oldSession != null)
            {
                _logger.LogInformation("▸ Архивация сессии '{Name}' (ID={Id})...", oldSession.Name, oldSession.Id);

                // Считаем статистику
                oldSession.ArchivedMessageCount = await db.Messages.CountAsync();
                oldSession.ArchivedPlayerCount = await db.Players
                    .Select(p => p.SteamId).Distinct().CountAsync();
                oldSession.ArchivedAt = DateTime.UtcNow;
                oldSession.IsActive = false;

                // ═══ Очистка рабочих таблиц (TRUNCATE через SQL) ═══
                _logger.LogInformation("▸ Очистка рабочих таблиц...");
                
                // Порядок важен: сначала dependent, потом parent
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE messages_denormalized CASCADE");
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE chat_participants CASCADE");
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE chats CASCADE");
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE faction_memberships CASCADE");
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE factions CASCADE");
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE pda_accounts CASCADE");
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE players CASCADE");

                // Сброс watermarks
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE sync_states SET last_synced_id = 0, last_synced_at = NULL, updated_at = NOW()");

                _logger.LogInformation("▸ Архивация завершена. Сообщений было: {Msgs}, Игроков: {Players}",
                    oldSession.ArchivedMessageCount, oldSession.ArchivedPlayerCount);
            }

            // ═══ 2. Создаём новую сессию ═══
            var newSession = new ServerSession
            {
                Name = sessionName,
                Description = description,
                MariaDbHost = host,
                MariaDbUser = user,
                InstanceNames = JsonSerializer.Serialize(instanceNames),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.ServerSessions.Add(newSession);
            await db.SaveChangesAsync();

            // ═══ 3. Обновляем system_settings ═══
            await UpsertSettingAsync(db, SettingKeys.MariaDbHost, host);
            await UpsertSettingAsync(db, SettingKeys.MariaDbUser, user);
            await UpsertSettingAsync(db, SettingKeys.MariaDbPassword, password);
            await UpsertSettingAsync(db, SettingKeys.MariaDbInstances, JsonSerializer.Serialize(instanceNames));
            await UpsertSettingAsync(db, SettingKeys.ActiveSessionId, newSession.Id.ToString());

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // ═══ 4. Инвалидируем кэш ═══
            InvalidateCache();

            _logger.LogInformation("═══ Новая сессия '{Name}' (ID={Id}) создана. ETL начнёт с чистого листа ═══",
                newSession.Name, newSession.Id);

            return newSession;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Первоначальный seed настроек из appsettings (только если system_settings пуста).
    /// </summary>
    public async Task SeedFromConfigAsync(IConfiguration config)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

        if (await db.SystemSettings.AnyAsync())
        {
            _logger.LogDebug("SystemSettings уже существуют, seed пропущен");
            return;
        }

        // Читаем из Syncer-style конфига (если есть)
        var instances = config.GetSection("MariaDbInstances").Get<List<MariaDbInstanceConfig>>();
        if (instances is null || instances.Count == 0)
        {
            _logger.LogInformation("Нет MariaDbInstances в appsettings — пропускаем seed настроек");
            return;
        }

        // Извлекаем host/user/password из первого connection string
        var firstCs = instances[0].ConnectionString;
        var csParams = ParseConnectionString(firstCs);

        var host = csParams.GetValueOrDefault("Server", "");
        var user = csParams.GetValueOrDefault("User", "");
        var password = csParams.GetValueOrDefault("Password", "");
        var instanceNames = instances.Select(i => i.Name).ToList();

        await UpsertSettingAsync(db, SettingKeys.MariaDbHost, host);
        await UpsertSettingAsync(db, SettingKeys.MariaDbUser, user);
        await UpsertSettingAsync(db, SettingKeys.MariaDbPassword, password);
        await UpsertSettingAsync(db, SettingKeys.MariaDbInstances, JsonSerializer.Serialize(instanceNames));

        var syncSettings = config.GetSection("SyncSettings").Get<SyncSettings>();
        if (syncSettings != null)
        {
            await UpsertSettingAsync(db, SettingKeys.SyncIntervalSeconds, syncSettings.IntervalSeconds.ToString());
            await UpsertSettingAsync(db, SettingKeys.SyncMessageBatchSize, syncSettings.MessageBatchSize.ToString());
        }

        // Создаём начальную сессию
        var session = new ServerSession
        {
            Name = "Начальная сессия",
            Description = "Автоматически создана из appsettings.json",
            MariaDbHost = host,
            MariaDbUser = user,
            InstanceNames = JsonSerializer.Serialize(instanceNames),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.ServerSessions.Add(session);
        await db.SaveChangesAsync();

        await UpsertSettingAsync(db, SettingKeys.ActiveSessionId, session.Id.ToString());
        await db.SaveChangesAsync();

        _logger.LogInformation("═══ Настройки перенесены из appsettings → PostgreSQL (сессия '{Name}') ═══", session.Name);
    }

    /// <summary>
    /// Получить список всех сессий (для истории).
    /// </summary>
    public async Task<List<ServerSession>> GetSessionsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        return await db.ServerSessions.OrderByDescending(s => s.Id).ToListAsync();
    }

    /// <summary>
    /// Получить настройки синхронизации.
    /// </summary>
    public async Task<(int intervalSeconds, int batchSize)> GetSyncSettingsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

        var settings = await db.SystemSettings
            .Where(s => s.Key == SettingKeys.SyncIntervalSeconds || s.Key == SettingKeys.SyncMessageBatchSize)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var interval = settings.TryGetValue(SettingKeys.SyncIntervalSeconds, out var iv) ? int.Parse(iv) : 15;
        var batch = settings.TryGetValue(SettingKeys.SyncMessageBatchSize, out var bs) ? int.Parse(bs) : 500;

        return (interval, batch);
    }

    /// <summary>
    /// Инвалидирует кэш (вызывается после смены настроек).
    /// </summary>
    public void InvalidateCache()
    {
        _cachedConnection = null;
        _cachedActiveSessionId = null;
        _lastCacheUpdate = DateTime.MinValue;
        SettingsVersion++;
        _logger.LogInformation("▸ Кэш настроек инвалидирован (v{Version})", SettingsVersion);
    }

    // ─── Generic setting access ──────────────────────────────

    /// <summary>
    /// Получить значение произвольной настройки по ключу.
    /// </summary>
    public async Task<string?> GetSettingAsync(string key)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    /// <summary>
    /// Сохранить (upsert) значение произвольной настройки.
    /// </summary>
    public async Task SetSettingAsync(string key, string value)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        await UpsertSettingAsync(db, key, value);
        await db.SaveChangesAsync();
    }

    // ─── Helpers ────────────────────────────────────────────

    private static async Task UpsertSettingAsync(AnalyticsDbContext db, string key, string value)
    {
        var existing = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    private static Dictionary<string, string> ParseConnectionString(string cs)
    {
        return cs.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => parts[0].Trim(),
                parts => parts[1].Trim(),
                StringComparer.OrdinalIgnoreCase);
    }
}
