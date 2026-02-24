using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Api.Services;

/// <summary>
/// Данные Steam-профиля, возвращаемые из Steam Web API (GetPlayerSummaries v2).
/// </summary>
public record SteamProfile(
    string SteamId,
    string? PersonaName,
    string? RealName,
    string? AvatarUrl,       // 32x32
    string? AvatarMediumUrl, // 64x64
    string? AvatarFullUrl,   // 184x184
    string? ProfileUrl,
    int? ProfileState,       // 0 = Offline, 1 = Online, 2 = Busy, etc.
    string? LocCountryCode,
    DateTime? LastLogoff
);

/// <summary>
/// Сервис для получения данных Steam-профилей.
/// Использует in-memory кеш (TTL 10 мин) + Steam Web API GetPlayerSummaries v2.
/// API Key читается из system_settings (ключ: steam.api_key).
/// </summary>
public class SteamService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SteamService> _logger;

    // Кеш: SteamID → (SteamProfile, CachedAt)
    private readonly ConcurrentDictionary<string, (SteamProfile Profile, DateTime CachedAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    // Кеш API key (чтобы не читать из БД каждый раз)
    private string? _cachedApiKey;
    private DateTime _apiKeyCachedAt = DateTime.MinValue;
    private static readonly TimeSpan ApiKeyCacheTtl = TimeSpan.FromMinutes(5);

    public SteamService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<SteamService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Получить Steam-профиль по SteamID64. Кешируется на 10 мин.
    /// Возвращает null если API key не настроен или Steam API недоступен.
    /// </summary>
    public async Task<SteamProfile?> GetProfileAsync(string steamId, CancellationToken ct = default)
    {
        // Проверяем кеш
        if (_cache.TryGetValue(steamId, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
            return cached.Profile;

        // Получаем API key
        var apiKey = await GetApiKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient("steam");
            var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={apiKey}&steamids={steamId}";
            
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Steam] API вернул {Status} для {SteamId}", response.StatusCode, steamId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var players = doc.RootElement
                .GetProperty("response")
                .GetProperty("players");

            if (players.GetArrayLength() == 0)
                return null;

            var p = players[0];

            var profile = new SteamProfile(
                SteamId: steamId,
                PersonaName: p.TryGetProperty("personaname", out var pn) ? pn.GetString() : null,
                RealName: p.TryGetProperty("realname", out var rn) ? rn.GetString() : null,
                AvatarUrl: p.TryGetProperty("avatar", out var av) ? av.GetString() : null,
                AvatarMediumUrl: p.TryGetProperty("avatarmedium", out var avm) ? avm.GetString() : null,
                AvatarFullUrl: p.TryGetProperty("avatarfull", out var avf) ? avf.GetString() : null,
                ProfileUrl: p.TryGetProperty("profileurl", out var pu) ? pu.GetString() : null,
                ProfileState: p.TryGetProperty("personastate", out var ps) ? ps.GetInt32() : null,
                LocCountryCode: p.TryGetProperty("loccountrycode", out var lc) ? lc.GetString() : null,
                LastLogoff: p.TryGetProperty("lastlogoff", out var lo)
                    ? DateTimeOffset.FromUnixTimeSeconds(lo.GetInt64()).UtcDateTime
                    : null
            );

            _cache[steamId] = (profile, DateTime.UtcNow);
            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Steam] Ошибка запроса для {SteamId}", steamId);
            return null;
        }
    }

    /// <summary>
    /// Пакетное получение Steam-профилей (до 100 за один запрос).
    /// </summary>
    public async Task<Dictionary<string, SteamProfile>> GetProfilesBatchAsync(
        IEnumerable<string> steamIds, CancellationToken ct = default)
    {
        var result = new Dictionary<string, SteamProfile>();
        var toFetch = new List<string>();

        foreach (var id in steamIds.Distinct())
        {
            if (_cache.TryGetValue(id, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
                result[id] = cached.Profile;
            else
                toFetch.Add(id);
        }

        if (toFetch.Count == 0) return result;

        var apiKey = await GetApiKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey)) return result;

        // Steam API принимает до 100 SteamID за раз
        foreach (var batch in toFetch.Chunk(100))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("steam");
                var ids = string.Join(",", batch);
                var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={apiKey}&steamids={ids}";

                var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(json);
                var players = doc.RootElement.GetProperty("response").GetProperty("players");

                foreach (var p in players.EnumerateArray())
                {
                    var sid = p.GetProperty("steamid").GetString()!;
                    var profile = new SteamProfile(
                        SteamId: sid,
                        PersonaName: p.TryGetProperty("personaname", out var pn) ? pn.GetString() : null,
                        RealName: p.TryGetProperty("realname", out var rn) ? rn.GetString() : null,
                        AvatarUrl: p.TryGetProperty("avatar", out var av) ? av.GetString() : null,
                        AvatarMediumUrl: p.TryGetProperty("avatarmedium", out var avm) ? avm.GetString() : null,
                        AvatarFullUrl: p.TryGetProperty("avatarfull", out var avf) ? avf.GetString() : null,
                        ProfileUrl: p.TryGetProperty("profileurl", out var pu) ? pu.GetString() : null,
                        ProfileState: p.TryGetProperty("personastate", out var ps) ? ps.GetInt32() : null,
                        LocCountryCode: p.TryGetProperty("loccountrycode", out var lc) ? lc.GetString() : null,
                        LastLogoff: p.TryGetProperty("lastlogoff", out var lo)
                            ? DateTimeOffset.FromUnixTimeSeconds(lo.GetInt64()).UtcDateTime
                            : null
                    );
                    _cache[sid] = (profile, DateTime.UtcNow);
                    result[sid] = profile;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Steam] Batch fetch error");
            }
        }

        return result;
    }

    private async Task<string?> GetApiKeyAsync(CancellationToken ct)
    {
        if (_cachedApiKey != null && DateTime.UtcNow - _apiKeyCachedAt < ApiKeyCacheTtl)
            return _cachedApiKey;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            var setting = await db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == SettingKeys.SteamApiKey, ct);

            _cachedApiKey = setting?.Value;
            _apiKeyCachedAt = DateTime.UtcNow;
            return _cachedApiKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Steam] Не удалось прочитать API key");
            return null;
        }
    }
}
