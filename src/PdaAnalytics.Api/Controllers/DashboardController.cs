using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Api.Dtos;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AnalyticsDbContext _db;

    public DashboardController(AnalyticsDbContext db) => _db = db;

    /// <summary>
    /// GET /api/dashboard/stats
    /// Общая статистика для дашборда.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats(CancellationToken ct = default)
    {
        var totalPlayers = await _db.Players.CountAsync(ct);
        var totalAccounts = await _db.PdaAccounts.CountAsync(ct);
        var totalMessages = await _db.Messages.CountAsync(ct);
        var totalPrivate = await _db.Messages.CountAsync(m => m.ChatType == ChatType.Private, ct);
        var totalGlobal = await _db.Messages.CountAsync(m => m.ChatType == ChatType.Global, ct);
        var totalFactions = await _db.Factions.CountAsync(ct);
        var totalInstances = await _db.Players.Select(p => p.SourceInstance).Distinct().CountAsync(ct);
        var lastMessage = await _db.Messages.MaxAsync(m => (DateTime?)m.SentAt, ct);

        return Ok(new DashboardStatsDto
        {
            TotalPlayers = totalPlayers,
            TotalPdaAccounts = totalAccounts,
            TotalMessages = totalMessages,
            TotalPrivateMessages = totalPrivate,
            TotalGlobalMessages = totalGlobal,
            TotalFactions = totalFactions,
            TotalInstances = totalInstances,
            LastMessageAt = lastMessage
        });
    }

    /// <summary>
    /// GET /api/dashboard/top-chatters?limit=10
    /// Топ игроков по количеству отправленных сообщений.
    /// </summary>
    [HttpGet("top-chatters")]
    public async Task<ActionResult<List<TopChatterDto>>> GetTopChatters(
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        var result = await _db.Messages
            .Where(m => m.SenderSteamId != null)
            .GroupBy(m => new { m.SenderSteamId, m.SenderNickname, m.SenderLogin })
            .Select(g => new TopChatterDto
            {
                SteamId = g.Key.SenderSteamId!,
                Nickname = g.Key.SenderNickname,
                Login = g.Key.SenderLogin,
                MessageCount = g.Count(),
                LastMessageAt = g.Max(m => m.SentAt)
            })
            .OrderByDescending(x => x.MessageCount)
            .Take(limit)
            .ToListAsync(ct);

        return Ok(result);
    }
}

public record TopChatterDto
{
    public required string SteamId { get; init; }
    public string? Nickname { get; init; }
    public string? Login { get; init; }
    public int MessageCount { get; init; }
    public DateTime LastMessageAt { get; init; }
}
