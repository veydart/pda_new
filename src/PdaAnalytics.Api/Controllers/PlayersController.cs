using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Api.Dtos;
using PdaAnalytics.Api.Services;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly AnalyticsDbContext _db;
    private readonly SteamService _steam;

    public PlayersController(AnalyticsDbContext db, SteamService steam)
    {
        _db = db;
        _steam = steam;
    }

    /// <summary>
    /// GET /api/players?search=...&instance=...&page=1&pageSize=20
    /// Поиск игроков по никнейму или SteamID (частичное совпадение).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PlayerSearchHit>>> GetPlayers(
        [FromQuery] string? search = null,
        [FromQuery] string? instance = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = _db.Players.AsQueryable();

        if (!string.IsNullOrWhiteSpace(instance))
            query = query.Where(p => p.SourceInstance == instance);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                (p.Nickname != null && EF.Functions.ILike(p.Nickname, $"%{term}%")) ||
                EF.Functions.ILike(p.SteamId, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.LastLogonDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PlayerSearchHit
            {
                SteamId = p.SteamId,
                Nickname = p.Nickname,
                SourceInstance = p.SourceInstance
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<PlayerSearchHit>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// GET /api/players/{steamId}
    /// Полный профиль игрока по SteamID: все PDA-аккаунты, фракции, контакты, статистика + Steam.
    /// Собирает данные со ВСЕХ инстансов.
    /// </summary>
    [HttpGet("{steamId}")]
    public async Task<ActionResult<PlayerProfileDto>> GetPlayerProfile(
        string steamId,
        CancellationToken ct = default)
    {
        // Берём игрока с первого инстанса, где он есть (для базовых данных)
        var player = await _db.Players
            .Where(p => p.SteamId == steamId)
            .OrderByDescending(p => p.LastLogonDate)
            .FirstOrDefaultAsync(ct);

        if (player == null)
            return NotFound(new { message = $"Игрок с SteamID {steamId} не найден" });

        // PDA-аккаунты со ВСЕХ инстансов
        var pdaAccounts = await _db.PdaAccounts
            .Where(a => a.SteamId == steamId)
            .OrderByDescending(a => a.LastActivity)
            .Select(a => new PdaAccountDto
            {
                SourceId = a.SourceId,
                Login = a.Login,
                LastActivity = a.LastActivity,
                SourceInstance = a.SourceInstance
            })
            .ToListAsync(ct);

        // Фракции
        var factions = await _db.FactionMemberships
            .Where(fm => fm.MemberSteamId == steamId)
            .Include(fm => fm.Faction)
            .Select(fm => new FactionMembershipDto
            {
                FactionName = fm.Faction!.Name,
                FactionColor = fm.Faction.Color,
                FactionIcon = fm.Faction.Icon,
                RankId = fm.RankId,
                SourceInstance = fm.SourceInstance
            })
            .ToListAsync(ct);

        // Статистика сообщений
        var sentCount = await _db.Messages
            .CountAsync(m => m.SenderSteamId == steamId, ct);

        var receivedCount = await _db.Messages
            .CountAsync(m => m.ReceiverSteamId == steamId, ct);

        // Контакты (уникальные собеседники)
        var contacts = await GetContactsAsync(steamId, ct);

        // ── Steam Profile (параллельно не блокирует — кеш 10 мин) ──
        var steamProfile = await _steam.GetProfileAsync(steamId, ct);

        return Ok(new PlayerProfileDto
        {
            SteamId = player.SteamId,
            Nickname = player.Nickname,
            RegistrationDate = player.RegistrationDate,
            LastLogonDate = player.LastLogonDate,
            SourceInstance = player.SourceInstance,
            // Steam
            SteamAvatarUrl = steamProfile?.AvatarFullUrl,
            SteamProfileUrl = steamProfile?.ProfileUrl,
            SteamPersonaName = steamProfile?.PersonaName,
            SteamRealName = steamProfile?.RealName,
            SteamCountryCode = steamProfile?.LocCountryCode,
            SteamPersonaState = steamProfile?.ProfileState,
            // Data
            PdaAccounts = pdaAccounts,
            Factions = factions,
            TotalMessagesSent = sentCount,
            TotalMessagesReceived = receivedCount,
            Contacts = contacts
        });
    }

    /// <summary>
    /// Находит всех уникальных собеседников игрока.
    /// </summary>
    private async Task<List<ContactDto>> GetContactsAsync(string steamId, CancellationToken ct)
    {
        // Собеседники из отправленных сообщений (receiver)
        var sentContacts = await _db.Messages
            .Where(m => m.SenderSteamId == steamId && m.ReceiverSteamId != null && m.ChatType == ChatType.Private)
            .GroupBy(m => new { m.ReceiverSteamId, m.ReceiverNickname })
            .Select(g => new
            {
                SteamId = g.Key.ReceiverSteamId!,
                Nickname = g.Key.ReceiverNickname,
                Count = g.Count(),
                LastMessage = g.Max(m => m.SentAt)
            })
            .ToListAsync(ct);

        // Собеседники из полученных сообщений (sender)
        var receivedContacts = await _db.Messages
            .Where(m => m.ReceiverSteamId == steamId && m.SenderSteamId != null && m.ChatType == ChatType.Private)
            .GroupBy(m => new { m.SenderSteamId, m.SenderNickname })
            .Select(g => new
            {
                SteamId = g.Key.SenderSteamId!,
                Nickname = g.Key.SenderNickname,
                Count = g.Count(),
                LastMessage = g.Max(m => m.SentAt)
            })
            .ToListAsync(ct);

        // Merge: объединяем sent + received по SteamID
        var merged = sentContacts.Concat(receivedContacts)
            .GroupBy(c => c.SteamId)
            .Select(g => new ContactDto
            {
                SteamId = g.Key,
                Nickname = g.Select(x => x.Nickname).FirstOrDefault(n => n != null),
                MessageCount = g.Sum(x => x.Count),
                LastMessageAt = g.Max(x => x.LastMessage)
            })
            .OrderByDescending(c => c.LastMessageAt)
            .Take(50) // Топ-50 контактов
            .ToList();

        return merged;
    }
}
