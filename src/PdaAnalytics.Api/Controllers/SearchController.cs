using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Api.Dtos;
using PdaAnalytics.Data;

namespace PdaAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly AnalyticsDbContext _db;

    public SearchController(AnalyticsDbContext db) => _db = db;

    /// <summary>
    /// GET /api/search?q=...&limit=20
    /// Omni-Search: ищет по SteamID, никнейму, логину PDA и тексту сообщений.
    /// Использует PostgreSQL trigram индексы для быстрого ILIKE.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SearchResultDto>> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 15,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new { message = "Запрос должен содержать минимум 2 символа" });

        var term = q.Trim();
        limit = Math.Clamp(limit, 1, 50);

        // Последовательные запросы (EF DbContext не потокобезопасен)
        var players = await SearchPlayersAsync(term, limit, ct);
        var accounts = await SearchPdaAccountsAsync(term, limit, ct);
        var messages = await SearchMessagesAsync(term, limit, ct);

        return Ok(new SearchResultDto
        {
            Players = players,
            PdaAccounts = accounts,
            Messages = messages
        });
    }

    private async Task<List<PlayerSearchHit>> SearchPlayersAsync(string term, int limit, CancellationToken ct)
    {
        return await _db.Players
            .Where(p =>
                EF.Functions.ILike(p.SteamId, $"%{term}%") ||
                (p.Nickname != null && EF.Functions.ILike(p.Nickname, $"%{term}%")))
            .OrderByDescending(p => p.LastLogonDate)
            .Take(limit)
            .Select(p => new PlayerSearchHit
            {
                SteamId = p.SteamId,
                Nickname = p.Nickname,
                SourceInstance = p.SourceInstance
            })
            .ToListAsync(ct);
    }

    private async Task<List<PdaAccountSearchHit>> SearchPdaAccountsAsync(string term, int limit, CancellationToken ct)
    {
        return await _db.PdaAccounts
            .Where(a =>
                EF.Functions.ILike(a.Login, $"%{term}%") ||
                EF.Functions.ILike(a.SteamId, $"%{term}%"))
            .OrderByDescending(a => a.LastActivity)
            .Take(limit)
            .Select(a => new PdaAccountSearchHit
            {
                SourceId = a.SourceId,
                Login = a.Login,
                SteamId = a.SteamId,
                SourceInstance = a.SourceInstance
            })
            .ToListAsync(ct);
    }

    private async Task<List<MessageSearchHit>> SearchMessagesAsync(string term, int limit, CancellationToken ct)
    {
        return await _db.Messages
            .Where(m => EF.Functions.ILike(m.Message, $"%{term}%"))
            .OrderByDescending(m => m.SentAt)
            .Take(limit)
            .Select(m => new MessageSearchHit
            {
                Id = m.Id,
                SenderLogin = m.SenderLogin,
                SenderSteamId = m.SenderSteamId,
                ReceiverLogin = m.ReceiverLogin,
                ReceiverSteamId = m.ReceiverSteamId,
                Message = m.Message,
                SentAt = m.SentAt,
                SourceInstance = m.SourceInstance
            })
            .ToListAsync(ct);
    }
}
