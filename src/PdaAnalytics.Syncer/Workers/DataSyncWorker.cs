using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Entities;
using PdaAnalytics.Syncer.Services;

namespace PdaAnalytics.Syncer.Workers;

/// <summary>
/// Фоновый ETL-воркер.
/// Перед каждым циклом читает настройки подключения MariaDB из PostgreSQL (system_settings).
/// После сохранения новых сообщений — передаёт их в DiscordDispatcherService (fire-and-forget).
/// </summary>
public class DataSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DiscordDispatcherService _discord;
    private readonly ILogger<DataSyncWorker> _logger;

    public DataSyncWorker(
        IServiceScopeFactory scopeFactory,
        DiscordDispatcherService discord,
        ILogger<DataSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _discord = discord;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("═══ DataSyncWorker запущен ═══");

        // Ждём немного, чтобы остальные компоненты (и миграции) успели стартовать
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Инициализация: БД + Discord-конфиги (с retry при ошибках)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureDatabaseCreatedAsync(stoppingToken);
                await _discord.ReloadConfigsAsync(stoppingToken);
                _logger.LogInformation("Инициализация завершена — переходим к синхронизации");
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка инициализации. Повтор через 10 сек...");
                await SafeDelay(10, stoppingToken);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // === Читаем настройки из PostgreSQL перед каждым циклом ===
            var (instances, intervalSeconds, batchSize) = await ReadSettingsFromDbAsync(stoppingToken);

            if (instances.Count == 0)
            {
                _logger.LogWarning("Нет настроек подключения MariaDB в system_settings. Ожидаем конфигурации...");
                await SafeDelay(15, stoppingToken);
                continue;
            }

            try
            {
                var allNewMessages = new List<MessageDenormalized>();

                foreach (var instance in instances)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
                        var syncService = scope.ServiceProvider.GetRequiredService<SyncService>();

                        var newMessages = await syncService.SyncInstanceAsync(
                            instance.Name,
                            instance.ConnectionString,
                            db,
                            batchSize,
                            stoppingToken);

                        allNewMessages.AddRange(newMessages);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[{Instance}] Ошибка синхронизации", instance.Name);
                    }
                }

                if (allNewMessages.Count > 0)
                {
                    _logger.LogInformation(
                        "══ Цикл синхронизации завершён. Всего новых сообщений: {Total} ══",
                        allNewMessages.Count);

                    // ── Discord: маршрутизируем в очередь — НЕ ждём HTTP ──
                    await _discord.EnqueueMessagesAsync(allNewMessages, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка в цикле синхронизации");
            }

            await SafeDelay(intervalSeconds, stoppingToken);
        }

        _logger.LogInformation("═══ DataSyncWorker остановлен ═══");
    }

    // ─── Helpers ────────────────────────────────────────────────

    private async Task<(List<Domain.Configuration.MariaDbInstanceConfig> instances, int interval, int batchSize)>
        ReadSettingsFromDbAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

            var settings = await db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);

            var host = settings.GetValueOrDefault(SettingKeys.MariaDbHost, "");
            var user = settings.GetValueOrDefault(SettingKeys.MariaDbUser, "");
            var password = settings.GetValueOrDefault(SettingKeys.MariaDbPassword, "");

            var instanceNames = settings.TryGetValue(SettingKeys.MariaDbInstances, out var instJson)
                ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(instJson) ?? []
                : [];

            var interval = settings.TryGetValue(SettingKeys.SyncIntervalSeconds, out var iv)
                ? int.TryParse(iv, out var i) ? i : 15
                : 15;

            var batchSize = settings.TryGetValue(SettingKeys.SyncMessageBatchSize, out var bs)
                ? int.TryParse(bs, out var b) ? b : 500
                : 500;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
                return ([], interval, batchSize);

            var instances = instanceNames.Select(name => new Domain.Configuration.MariaDbInstanceConfig
            {
                Name = name,
                ConnectionString = $"Server={host};Database={name};User={user};Password={password};SslMode=None;"
            }).ToList();

            return (instances, interval, batchSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка чтения настроек из system_settings");
            return ([], 15, 500);
        }
    }

    private async Task EnsureDatabaseCreatedAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            // Не запускаем MigrateAsync — это делает API-процесс.
            // Syncer только проверяет подключение к БД.
            var canConnect = await db.Database.CanConnectAsync(ct);
            if (!canConnect)
                throw new InvalidOperationException("Не удаётся подключиться к PostgreSQL");
            _logger.LogInformation("PostgreSQL подключение проверено — OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при подключении к PostgreSQL");
            throw;
        }
    }

    private async Task SafeDelay(int seconds, CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(seconds), ct); }
        catch (OperationCanceledException) { }
    }
}
