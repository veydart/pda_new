using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Api.Dtos;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AnalyticsDbContext _db;

    public MessagesController(AnalyticsDbContext db) => _db = db;

    /// <summary>
    /// GET /api/messages/between?steamId1=...&steamId2=...&page=1&pageSize=50
    /// История переписки между двумя игроками (по SteamID).
    /// Использует денормализованные индексы для мгновенного поиска.
    /// </summary>
    [HttpGet("between")]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetConversation(
        [FromQuery] string steamId1,
        [FromQuery] string steamId2,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(steamId1) || string.IsNullOrWhiteSpace(steamId2))
            return BadRequest(new { message = "Оба steamId1 и steamId2 обязательны" });

        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        // Двусторонний поиск: A→B и B→A
        var query = _db.Messages
            .Where(m =>
                (m.SenderSteamId == steamId1 && m.ReceiverSteamId == steamId2) ||
                (m.SenderSteamId == steamId2 && m.ReceiverSteamId == steamId1));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => ToMessageDto(m))
            .ToListAsync(ct);

        return Ok(new PagedResult<MessageDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// GET /api/messages/feed?page=1&pageSize=50&instance=...&type=...
    /// Live Feed — последние сообщения (для ленты на фронтенде).
    /// </summary>
    [HttpGet("feed")]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? instance = null,
        [FromQuery] string? type = null,  // "private" or "global"
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var query = _db.Messages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(instance))
            query = query.Where(m => m.SourceInstance == instance);

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (type.Equals("private", StringComparison.OrdinalIgnoreCase))
                query = query.Where(m => m.ChatType == ChatType.Private);
            else if (type.Equals("global", StringComparison.OrdinalIgnoreCase))
                query = query.Where(m => m.ChatType == ChatType.Global);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => ToMessageDto(m))
            .ToListAsync(ct);

        return Ok(new PagedResult<MessageDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// GET /api/messages/by-player/{steamId}?page=1&pageSize=50
    /// Все сообщения, отправленные или полученные игроком.
    /// </summary>
    [HttpGet("by-player/{steamId}")]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetByPlayer(
        string steamId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? direction = null, // "sent", "received", null = both
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var query = _db.Messages.AsQueryable();

        query = direction?.ToLower() switch
        {
            "sent" => query.Where(m => m.SenderSteamId == steamId),
            "received" => query.Where(m => m.ReceiverSteamId == steamId),
            _ => query.Where(m => m.SenderSteamId == steamId || m.ReceiverSteamId == steamId)
        };

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => ToMessageDto(m))
            .ToListAsync(ct);

        return Ok(new PagedResult<MessageDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// GET /api/messages/graph/{steamId}
    /// Данные для графа связей: все уникальные связи игрока (кто кому).
    /// </summary>
    [HttpGet("graph/{steamId}")]
    public async Task<ActionResult<GraphDataDto>> GetGraph(
        string steamId,
        [FromQuery] int depth = 1,   // 1 = прямые контакты, 2 = контакты контактов
        [FromQuery] int maxNodes = 50,
        CancellationToken ct = default)
    {
        depth = Math.Clamp(depth, 1, 2);
        maxNodes = Math.Clamp(maxNodes, 5, 200);

        var nodes = new Dictionary<string, GraphNodeDto>();
        var edges = new List<GraphEdgeDto>();
        var processed = new HashSet<string>();

        await BuildGraphLevel(steamId, nodes, edges, processed, depth, maxNodes, ct);

        return Ok(new GraphDataDto
        {
            Nodes = nodes.Values.ToList(),
            Edges = edges
        });
    }

    private async Task BuildGraphLevel(
        string steamId,
        Dictionary<string, GraphNodeDto> nodes,
        List<GraphEdgeDto> edges,
        HashSet<string> processed,
        int remainingDepth,
        int maxNodes,
        CancellationToken ct)
    {
        if (remainingDepth <= 0 || processed.Contains(steamId) || nodes.Count >= maxNodes)
            return;

        processed.Add(steamId);

        // Добавляем узел
        if (!nodes.ContainsKey(steamId))
        {
            var player = await _db.Players
                .Where(p => p.SteamId == steamId)
                .Select(p => new { p.Nickname })
                .FirstOrDefaultAsync(ct);

            nodes[steamId] = new GraphNodeDto
            {
                SteamId = steamId,
                Nickname = player?.Nickname ?? steamId,
                IsCenter = remainingDepth == 1 && processed.Count == 1
            };
        }

        // Находим связи (отправленные)
        var sentEdges = await _db.Messages
            .Where(m => m.SenderSteamId == steamId && m.ReceiverSteamId != null && m.ChatType == ChatType.Private)
            .GroupBy(m => m.ReceiverSteamId)
            .Select(g => new { TargetSteamId = g.Key!, MessageCount = g.Count(), LastMessage = g.Max(m => m.SentAt) })
            .OrderByDescending(g => g.MessageCount)
            .Take(maxNodes)
            .ToListAsync(ct);

        // Полученные
        var receivedEdges = await _db.Messages
            .Where(m => m.ReceiverSteamId == steamId && m.SenderSteamId != null && m.ChatType == ChatType.Private)
            .GroupBy(m => m.SenderSteamId)
            .Select(g => new { TargetSteamId = g.Key!, MessageCount = g.Count(), LastMessage = g.Max(m => m.SentAt) })
            .OrderByDescending(g => g.MessageCount)
            .Take(maxNodes)
            .ToListAsync(ct);

        // Merge edges
        var allEdges = sentEdges.Concat(receivedEdges)
            .GroupBy(e => e.TargetSteamId)
            .Select(g => new
            {
                TargetSteamId = g.Key,
                MessageCount = g.Sum(x => x.MessageCount),
                LastMessage = g.Max(x => x.LastMessage)
            })
            .OrderByDescending(e => e.MessageCount)
            .Take(maxNodes - nodes.Count)
            .ToList();

        foreach (var edge in allEdges)
        {
            if (nodes.Count >= maxNodes) break;

            // Добавляем узел контакта
            if (!nodes.ContainsKey(edge.TargetSteamId))
            {
                var contactPlayer = await _db.Players
                    .Where(p => p.SteamId == edge.TargetSteamId)
                    .Select(p => new { p.Nickname })
                    .FirstOrDefaultAsync(ct);

                nodes[edge.TargetSteamId] = new GraphNodeDto
                {
                    SteamId = edge.TargetSteamId,
                    Nickname = contactPlayer?.Nickname ?? edge.TargetSteamId,
                    IsCenter = false
                };
            }

            // Добавляем ребро (избегаем дубликатов)
            var edgeKey = string.Compare(steamId, edge.TargetSteamId, StringComparison.Ordinal) < 0
                ? $"{steamId}-{edge.TargetSteamId}"
                : $"{edge.TargetSteamId}-{steamId}";

            if (!edges.Any(e => e.Id == edgeKey))
            {
                edges.Add(new GraphEdgeDto
                {
                    Id = edgeKey,
                    Source = steamId,
                    Target = edge.TargetSteamId,
                    Weight = edge.MessageCount,
                    LastMessageAt = edge.LastMessage
                });
            }
        }

        // Рекурсия для глубины 2
        if (remainingDepth > 1)
        {
            foreach (var edge in allEdges.Take(10)) // Ограничиваем рекурсию
            {
                if (nodes.Count >= maxNodes) break;
                await BuildGraphLevel(edge.TargetSteamId, nodes, edges, processed, remainingDepth - 1, maxNodes, ct);
            }
        }
    }

    // ─── Маппинг ──────────────────────────────────────────────────

    private static MessageDto ToMessageDto(MessageDenormalized m) => new()
    {
        Id = m.Id,
        SourceMessageId = m.SourceMessageId,
        ChatType = m.ChatType.ToString(),
        ChatName = m.ChatName,
        SenderLogin = m.SenderLogin,
        SenderSteamId = m.SenderSteamId,
        SenderNickname = m.SenderNickname,
        ReceiverLogin = m.ReceiverLogin,
        ReceiverSteamId = m.ReceiverSteamId,
        ReceiverNickname = m.ReceiverNickname,
        Message = m.Message,
        Attachments = m.Attachments,
        SentAt = m.SentAt,
        SourceInstance = m.SourceInstance
    };
}

// ─── Graph DTOs ──────────────────────────────────────────────────

public record GraphDataDto
{
    public List<GraphNodeDto> Nodes { get; init; } = [];
    public List<GraphEdgeDto> Edges { get; init; } = [];
}

public record GraphNodeDto
{
    public required string SteamId { get; init; }
    public string? Nickname { get; init; }
    public bool IsCenter { get; init; }
}

public record GraphEdgeDto
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Target { get; init; }
    public int Weight { get; init; }
    public DateTime LastMessageAt { get; init; }
}
