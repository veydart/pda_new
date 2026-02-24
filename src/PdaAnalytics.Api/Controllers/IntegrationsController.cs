using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Entities;
using System.Text.Json;

namespace PdaAnalytics.Api.Controllers;

// ═══════════════════════════════════════════════════════════════
//  DTOs
// ═══════════════════════════════════════════════════════════════

// ── ChatWebhook ──

public record ChatWebhookDto(
    int Id,
    int ChatSourceId,
    string SourceInstance,
    string? ChatName,
    string WebhookUrl,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateChatWebhookRequest(
    int ChatSourceId,
    string SourceInstance,
    string? ChatName,
    string WebhookUrl);

public record UpdateChatWebhookRequest(
    string? ChatName,
    string? WebhookUrl,
    bool? IsEnabled);

// ── FactionMention ──

public record FactionMentionDto(
    int Id,
    int? FactionSourceId,
    string? SourceInstance,
    string DisplayName,
    string[] Aliases,
    string? DiscordRoleId,
    string WebhookUrl,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateFactionMentionRequest(
    int? FactionSourceId,
    string? SourceInstance,
    string DisplayName,
    string[] Aliases,
    string? DiscordRoleId,
    string WebhookUrl);

public record UpdateFactionMentionRequest(
    string? DisplayName,
    string[]? Aliases,
    string? DiscordRoleId,
    string? WebhookUrl,
    bool? IsEnabled);

// ── Справочники ──

public record ChatRefDto(int SourceId, string? Name, string Type, string SourceInstance);
public record FactionRefDto(int SourceId, string Name, string? Icon, string SourceInstance);

// ═══════════════════════════════════════════════════════════════
//  Controller
// ═══════════════════════════════════════════════════════════════

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IntegrationsController : ControllerBase
{
    private readonly AnalyticsDbContext _db;

    public IntegrationsController(AnalyticsDbContext db) => _db = db;

    // ─────────────────────────────────────────────────────────────
    //  CHAT WEBHOOKS — CRUD
    // ─────────────────────────────────────────────────────────────

    /// <summary>Список всех правил трансляции чатов.</summary>
    [HttpGet("chat-webhooks")]
    public async Task<IActionResult> GetChatWebhooks()
    {
        var list = await _db.ChatWebhookConfigs
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ChatWebhookDto(
                c.Id, c.ChatSourceId, c.SourceInstance,
                c.ChatName, c.WebhookUrl, c.IsEnabled,
                c.CreatedAt, c.UpdatedAt))
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Создать правило трансляции чата.</summary>
    [HttpPost("chat-webhooks")]
    public async Task<IActionResult> CreateChatWebhook([FromBody] CreateChatWebhookRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.WebhookUrl))
            return BadRequest(new { message = "WebhookUrl обязателен" });

        if (string.IsNullOrWhiteSpace(req.SourceInstance))
            return BadRequest(new { message = "SourceInstance обязателен" });

        // Проверяем дубликат
        var exists = await _db.ChatWebhookConfigs.AnyAsync(c =>
            c.ChatSourceId == req.ChatSourceId && c.SourceInstance == req.SourceInstance);
        if (exists)
            return Conflict(new { message = $"Webhook для чата #{req.ChatSourceId} [{req.SourceInstance}] уже существует" });

        var entity = new ChatWebhookConfig
        {
            ChatSourceId = req.ChatSourceId,
            SourceInstance = req.SourceInstance,
            ChatName = req.ChatName,
            WebhookUrl = req.WebhookUrl,
            IsEnabled = true,
        };

        _db.ChatWebhookConfigs.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new ChatWebhookDto(
            entity.Id, entity.ChatSourceId, entity.SourceInstance,
            entity.ChatName, entity.WebhookUrl, entity.IsEnabled,
            entity.CreatedAt, entity.UpdatedAt));
    }

    /// <summary>Обновить правило трансляции чата.</summary>
    [HttpPut("chat-webhooks/{id:int}")]
    public async Task<IActionResult> UpdateChatWebhook(int id, [FromBody] UpdateChatWebhookRequest req)
    {
        var entity = await _db.ChatWebhookConfigs.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Правило не найдено" });

        if (req.ChatName != null) entity.ChatName = req.ChatName;
        if (req.WebhookUrl != null) entity.WebhookUrl = req.WebhookUrl;
        if (req.IsEnabled.HasValue) entity.IsEnabled = req.IsEnabled.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ChatWebhookDto(
            entity.Id, entity.ChatSourceId, entity.SourceInstance,
            entity.ChatName, entity.WebhookUrl, entity.IsEnabled,
            entity.CreatedAt, entity.UpdatedAt));
    }

    /// <summary>Удалить правило трансляции чата.</summary>
    [HttpDelete("chat-webhooks/{id:int}")]
    public async Task<IActionResult> DeleteChatWebhook(int id)
    {
        var entity = await _db.ChatWebhookConfigs.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Правило не найдено" });

        _db.ChatWebhookConfigs.Remove(entity);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Удалено" });
    }

    // ─────────────────────────────────────────────────────────────
    //  FACTION MENTIONS — CRUD
    // ─────────────────────────────────────────────────────────────

    /// <summary>Список всех правил упоминаний фракций.</summary>
    [HttpGet("faction-mentions")]
    public async Task<IActionResult> GetFactionMentions()
    {
        var list = await _db.FactionMentionConfigs
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return Ok(list.Select(f => ToMentionDto(f)));
    }

    /// <summary>Создать правило упоминания фракции.</summary>
    [HttpPost("faction-mentions")]
    public async Task<IActionResult> CreateFactionMention([FromBody] CreateFactionMentionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.WebhookUrl))
            return BadRequest(new { message = "WebhookUrl обязателен" });
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { message = "DisplayName обязателен" });
        if (req.Aliases == null || req.Aliases.Length == 0)
            return BadRequest(new { message = "Необходим хотя бы один алиас" });

        var entity = new FactionMentionConfig
        {
            FactionSourceId = req.FactionSourceId,
            SourceInstance = req.SourceInstance,
            DisplayName = req.DisplayName,
            AliasesJson = JsonSerializer.Serialize(req.Aliases),
            DiscordRoleId = string.IsNullOrWhiteSpace(req.DiscordRoleId) ? null : req.DiscordRoleId.Trim(),
            WebhookUrl = req.WebhookUrl,
            IsEnabled = true,
        };

        _db.FactionMentionConfigs.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(ToMentionDto(entity));
    }

    /// <summary>Обновить правило упоминания фракции.</summary>
    [HttpPut("faction-mentions/{id:int}")]
    public async Task<IActionResult> UpdateFactionMention(int id, [FromBody] UpdateFactionMentionRequest req)
    {
        var entity = await _db.FactionMentionConfigs.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Правило не найдено" });

        if (req.DisplayName != null) entity.DisplayName = req.DisplayName;
        if (req.Aliases != null) entity.AliasesJson = JsonSerializer.Serialize(req.Aliases);
        if (req.DiscordRoleId != null) entity.DiscordRoleId = string.IsNullOrWhiteSpace(req.DiscordRoleId) ? null : req.DiscordRoleId.Trim();
        if (req.WebhookUrl != null) entity.WebhookUrl = req.WebhookUrl;
        if (req.IsEnabled.HasValue) entity.IsEnabled = req.IsEnabled.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToMentionDto(entity));
    }

    /// <summary>Удалить правило упоминания фракции.</summary>
    [HttpDelete("faction-mentions/{id:int}")]
    public async Task<IActionResult> DeleteFactionMention(int id)
    {
        var entity = await _db.FactionMentionConfigs.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Правило не найдено" });

        _db.FactionMentionConfigs.Remove(entity);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Удалено" });
    }

    // ─────────────────────────────────────────────────────────────
    //  СПРАВОЧНИКИ — для dropdowns в UI
    // ─────────────────────────────────────────────────────────────

    /// <summary>Все глобальные чаты (для выбора при создании ChatWebhook).</summary>
    [HttpGet("ref/chats")]
    public async Task<IActionResult> GetChatsRef()
    {
        var chats = await _db.Chats
            .Where(c => c.Type == ChatType.Global)
            .OrderBy(c => c.SourceInstance)
            .ThenBy(c => c.Name)
            .Select(c => new ChatRefDto(c.SourceId, c.Name, c.Type.ToString(), c.SourceInstance))
            .ToListAsync();

        return Ok(chats);
    }

    /// <summary>Все фракции (для выбора при создании FactionMention).</summary>
    [HttpGet("ref/factions")]
    public async Task<IActionResult> GetFactionsRef()
    {
        var factions = await _db.Factions
            .OrderBy(f => f.SourceInstance)
            .ThenBy(f => f.Name)
            .Select(f => new FactionRefDto(f.SourceId, f.Name, f.Icon, f.SourceInstance))
            .ToListAsync();

        return Ok(factions);
    }

    // ─────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    private static FactionMentionDto ToMentionDto(FactionMentionConfig f)
    {
        string[] aliases;
        try { aliases = JsonSerializer.Deserialize<string[]>(f.AliasesJson) ?? []; }
        catch { aliases = []; }

        return new FactionMentionDto(
            f.Id, f.FactionSourceId, f.SourceInstance,
            f.DisplayName, aliases, f.DiscordRoleId, f.WebhookUrl,
            f.IsEnabled, f.CreatedAt, f.UpdatedAt);
    }
}
