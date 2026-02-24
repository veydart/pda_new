using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Syncer.Services;

// ═══════════════════════════════════════════════════════════════
//  QUEUE  — Singleton, шарится между DataSyncWorker и Dispatcher
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Очередь Discord-событий на основе System.Threading.Channels.
/// Bounded(4096) + DropOldest обеспечивает backpressure без блокировки ETL.
/// </summary>
public class DiscordQueue
{
    private readonly Channel<DiscordDispatchEvent> _channel =
        Channel.CreateBounded<DiscordDispatchEvent>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

    public ChannelWriter<DiscordDispatchEvent> Writer => _channel.Writer;
    public ChannelReader<DiscordDispatchEvent> Reader => _channel.Reader;

    /// <summary>Fire-and-forget, не блокирует вызывающий поток.</summary>
    public bool TryPublish(DiscordDispatchEvent evt) => Writer.TryWrite(evt);
}

// ═══════════════════════════════════════════════════════════════
//  MENTION PARSER
// ═══════════════════════════════════════════════════════════════

public record MentionMatch(string WebhookUrl, string FactionName, string MatchedAlias, string? DiscordRoleId);

/// <summary>
/// Парсер упоминаний. Компилирует один Regex на конфиг и ищет
/// "@alias" или "\balias\b" (регистронезависимо).
/// Кэш инвалидируется по версии из DiscordDispatcherService.
/// </summary>
public class DiscordMentionParser
{
    private readonly ILogger<DiscordMentionParser> _logger;
    private readonly Dictionary<int, (Regex regex, string factionName, string webhookUrl, string? discordRoleId)> _compiled = new();
    private long _cacheVersion = -1;

    public DiscordMentionParser(ILogger<DiscordMentionParser> logger) => _logger = logger;

    public IReadOnlyList<MentionMatch> Parse(string text, IEnumerable<FactionMentionConfig> configs, long version)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        if (version != _cacheVersion)
            Rebuild(configs, version);

        var results = new List<MentionMatch>();
        foreach (var (_, (regex, faction, webhook, roleId)) in _compiled)
        {
            var m = regex.Match(text);
            if (m.Success)
                results.Add(new MentionMatch(webhook, faction, m.Value, roleId));
        }
        return results;
    }

    private void Rebuild(IEnumerable<FactionMentionConfig> configs, long version)
    {
        _compiled.Clear();
        foreach (var cfg in configs.Where(c => c.IsEnabled))
        {
            try
            {
                var aliases = ParseAliases(cfg.AliasesJson);
                if (aliases.Length == 0) continue;

                var patterns = aliases.Select(a =>
                {
                    var esc = Regex.Escape(a.TrimStart('@'));
                    return $@"@{esc}|\b{esc}\b";
                });

                var regex = new Regex(
                    string.Join("|", patterns),
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromMilliseconds(50));

                _compiled[cfg.Id] = (regex, cfg.DisplayName, cfg.WebhookUrl, cfg.DiscordRoleId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MentionParser] Ошибка regex для '{Name}'", cfg.DisplayName);
            }
        }
        _cacheVersion = version;
        _logger.LogInformation("[MentionParser] Перестроен: {Count} правил (v{V})", _compiled.Count, version);
    }

    internal static string[] ParseAliases(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch { return []; }
    }
}

// ═══════════════════════════════════════════════════════════════
//  DISPATCHER  — BackgroundService
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Читает события из DiscordQueue и отправляет их в Discord Webhooks.
///
/// Связь с DataSyncWorker: после сохранения новых сообщений в PostgreSQL
/// DataSyncWorker вызывает EnqueueMessages(newMessages) — синхронно, без ожидания HTTP.
///
/// Rate-limit (429): ждём Retry-After из заголовка.
/// Server errors (5xx): 3 попытки с экспоненциальным backoff.
/// Кэш конфигов обновляется раз в цикл (каждые ReloadIntervalSeconds секунд).
/// </summary>
public class DiscordDispatcherService : BackgroundService
{
    private readonly DiscordQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly DiscordMentionParser _mentionParser;
    private readonly ILogger<DiscordDispatcherService> _logger;

    // In-memory кэш конфигов (обновляется из БД)
    private Dictionary<(int chatId, string instance), string> _chatCache = new();
    private List<FactionMentionConfig> _mentionCache = [];
    private long _configVersion;
    private DateTime _lastConfigReload = DateTime.MinValue;
    private const int ReloadIntervalSeconds = 30;

    // Статистика
    private long _sent;
    private long _errors;

    public DiscordDispatcherService(
        DiscordQueue queue,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        DiscordMentionParser mentionParser,
        ILogger<DiscordDispatcherService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _mentionParser = mentionParser;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("═══ DiscordDispatcherService запущен ═══");
        await ReloadConfigsAsync(ct);

        await foreach (var evt in _queue.Reader.ReadAllAsync(ct))
        {
            try
            {
                await SendAsync(evt, ct);
                _sent++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _errors++;
                _logger.LogError(ex, "[Discord] Dispatch error #{N}", _errors);
            }
        }

        _logger.LogInformation("═══ DiscordDispatcherService остановлен. Отправлено: {N}, Ошибок: {E} ═══", _sent, _errors);
    }

    // ── Public API для DataSyncWorker ─────────────────────────────

    /// <summary>
    /// Вызывается из DataSyncWorker после каждого цикла синхронизации.
    /// Маршрутизирует новые сообщения в очередь — не блокирует ETL.
    /// </summary>
    public async Task EnqueueMessagesAsync(IEnumerable<MessageDenormalized> messages, CancellationToken ct)
    {
        // Периодически перезагружаем конфиги из БД
        if ((DateTime.UtcNow - _lastConfigReload).TotalSeconds >= ReloadIntervalSeconds)
            await ReloadConfigsAsync(ct);

        int chatPushed = 0, mentionPushed = 0;

        foreach (var msg in messages)
        {
            // 1. ChatBroadcast?
            if (_chatCache.TryGetValue((msg.SourceChatId, msg.SourceInstance), out var chatUrl))
            {
                _queue.TryPublish(new DiscordDispatchEvent
                {
                    EventType = DiscordEventType.ChatBroadcast,
                    WebhookUrl = chatUrl,
                    Message = msg,
                });
                chatPushed++;
            }

            // 2. FactionMention?
            if (!string.IsNullOrWhiteSpace(msg.Message))
            {
                var matches = _mentionParser.Parse(msg.Message, _mentionCache, _configVersion);
                foreach (var match in matches)
                {
                    _queue.TryPublish(new DiscordDispatchEvent
                    {
                        EventType = DiscordEventType.FactionMention,
                        WebhookUrl = match.WebhookUrl,
                        Message = msg,
                        MentionedFactionName = match.FactionName,
                        DiscordRoleId = match.DiscordRoleId,
                    });
                    mentionPushed++;
                }
            }
        }

        if (chatPushed + mentionPushed > 0)
            _logger.LogInformation("[Discord] Поставлено в очередь: {Chat} трансляций, {Mention} упоминаний",
                chatPushed, mentionPushed);
    }

    // ── Config reload ─────────────────────────────────────────────

    public async Task ReloadConfigsAsync(CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

            var chatConfigs = await db.ChatWebhookConfigs.Where(c => c.IsEnabled).ToListAsync(ct);
            var mentionConfigs = await db.FactionMentionConfigs.Where(f => f.IsEnabled).ToListAsync(ct);

            _chatCache = chatConfigs.ToDictionary(
                c => (c.ChatSourceId, c.SourceInstance),
                c => c.WebhookUrl);

            _mentionCache = mentionConfigs;
            _configVersion++;
            _lastConfigReload = DateTime.UtcNow;

            _logger.LogDebug("[Discord] Конфиги: {C} чатов, {M} упоминаний (v{V})",
                chatConfigs.Count, mentionConfigs.Count, _configVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discord] Ошибка перезагрузки конфигов");
        }
    }

    // ── HTTP Send ─────────────────────────────────────────────────

    private async Task SendAsync(DiscordDispatchEvent evt, CancellationToken ct)
    {
        var payload = BuildPayload(evt);
        var body = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        var http = _httpFactory.CreateClient("discord");
        const int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var response = await http.PostAsync(evt.WebhookUrl, body, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("[Discord] ✓ {Type}", evt.EventType);
                return;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                _logger.LogWarning("[Discord] 429 RateLimit. Пауза {S}s (попытка {A}/{M})",
                    wait.TotalSeconds, attempt, maxAttempts);
                await Task.Delay(wait, ct);
                continue;
            }

            if ((int)response.StatusCode >= 500)
            {
                var wait = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning("[Discord] {Code} Server Error. Ретрай через {S}s",
                    (int)response.StatusCode, wait.TotalSeconds);
                await Task.Delay(wait, ct);
                continue;
            }

            // 4xx (не 429) — не ретраим
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("[Discord] HTTP {Code}: {Err}", (int)response.StatusCode,
                err.Length > 200 ? err[..200] : err);
            return;
        }

        _logger.LogError("[Discord] Исчерпаны все {M} попытки для {Type}", maxAttempts, evt.EventType);
    }

    // ── Payload Builders ──────────────────────────────────────────

    private static object BuildPayload(DiscordDispatchEvent evt)
    {
        var msg = evt.Message;
        var sender = msg.SenderNickname ?? msg.SenderLogin ?? "Неизвестный";
        var chat = msg.ChatName ?? $"Chat#{msg.SourceChatId}";
        var time = msg.SentAt.ToString("HH:mm:ss");
        var inst = msg.SourceInstance;
        var text = msg.Message.Length > 2000 ? msg.Message[..1997] + "…" : msg.Message;

        return evt.EventType switch
        {
            DiscordEventType.ChatBroadcast => new
            {
                username = $"PDA — {chat}",
                embeds = new[]
                {
                    new
                    {
                        color = 0x38bdf8,
                        author = new { name = $"{sender}  •  {time}  [{inst}]" },
                        description = text,
                        footer = new { text = $"📡 {chat}" }
                    }
                }
            },
            DiscordEventType.FactionMention => new
            {
                // Role ping — Discord парсит <@&ID> только из content, не из embeds
                content = !string.IsNullOrWhiteSpace(evt.DiscordRoleId)
                    ? $"<@&{evt.DiscordRoleId}>"
                    : (string?)null,
                username = $"PDA — @{evt.MentionedFactionName}",
                embeds = new[]
                {
                    new
                    {
                        color = 0xf472b6,
                        title = $"📢 Упоминание: {evt.MentionedFactionName}",
                        author = new { name = $"{sender}  •  {time}  [{inst}]" },
                        description = text,
                        footer = new { text = "📡 PDA Analytics • Mention Alert" }
                    }
                }
            },
            _ => new { content = $"[PDA] {text}" }
        };
    }
}
