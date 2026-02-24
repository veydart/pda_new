using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdaAnalytics.Api.Services;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Api.Controllers;

/// <summary>
/// Управление серверными настройками (только SuperAdmin).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.SuperAdmin))]
public class SettingsController : ControllerBase
{
    private readonly ServerSettingsService _settings;

    public SettingsController(ServerSettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// GET /api/settings/connection
    /// Текущие настройки подключения MariaDB (пароль маскируется).
    /// </summary>
    [HttpGet("connection")]
    public async Task<IActionResult> GetConnection()
    {
        var info = await _settings.GetConnectionInfoAsync();
        return Ok(new
        {
            host = info.Host,
            user = info.User,
            password = info.Password.Length > 0 ? "••••••" : "",
            instanceNames = info.InstanceNames,
            isConfigured = info.IsConfigured
        });
    }

    /// <summary>
    /// POST /api/settings/connection
    /// Сохранить новое подключение MariaDB.
    /// Архивирует старые данные, создаёт новую сессию, сбрасывает watermarks.
    /// ETL-воркер начнёт синхронизацию с чистого листа.
    /// </summary>
    [HttpPost("connection")]
    public async Task<IActionResult> SaveConnection([FromBody] SaveConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Host))
            return BadRequest(new { message = "Хост не может быть пустым" });
        if (string.IsNullOrWhiteSpace(request.User))
            return BadRequest(new { message = "Логин не может быть пустым" });
        if (request.InstanceNames is null || request.InstanceNames.Count == 0)
            return BadRequest(new { message = "Нужно указать хотя бы один инстанс" });
        if (string.IsNullOrWhiteSpace(request.SessionName))
            return BadRequest(new { message = "Нужно указать имя сессии" });

        try
        {
            var session = await _settings.SaveConnectionAndArchiveAsync(
                request.Host, request.User, request.Password,
                request.InstanceNames, request.SessionName, request.Description);

            return Ok(new
            {
                message = "Подключение сохранено. Старые данные архивированы. ETL начнёт заново.",
                session = new
                {
                    session.Id,
                    session.Name,
                    session.MariaDbHost,
                    session.InstanceNames,
                    session.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Ошибка: {ex.Message}" });
        }
    }

    /// <summary>
    /// GET /api/settings/sessions
    /// История всех серверных сессий (текущая + архивные).
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _settings.GetSessionsAsync();
        return Ok(sessions.Select(s => new
        {
            s.Id,
            s.Name,
            s.Description,
            s.MariaDbHost,
            s.MariaDbUser,
            s.InstanceNames,
            s.IsActive,
            s.CreatedAt,
            s.ArchivedAt,
            s.ArchivedMessageCount,
            s.ArchivedPlayerCount
        }));
    }

    /// <summary>
    /// GET /api/settings/sync
    /// Настройки синхронизации (интервал, batch size).
    /// </summary>
    [HttpGet("sync")]
    public async Task<IActionResult> GetSyncSettings()
    {
        var (interval, batch) = await _settings.GetSyncSettingsAsync();
        return Ok(new { intervalSeconds = interval, messageBatchSize = batch });
    }

    /// <summary>
    /// POST /api/settings/test-connection
    /// Тестирование подключения к MariaDB (проверяет, что можно коннектиться).
    /// </summary>
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] TestConnectionRequest request)
    {
        try
        {
            var connStr = $"Server={request.Host};Database={request.TestDatabase ?? "information_schema"};User={request.User};Password={request.Password};SslMode=None;Connect Timeout=5";
            
            using var conn = new MySqlConnector.MySqlConnection(connStr);
            await conn.OpenAsync();

            // Получаем список баз
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SHOW DATABASES";
            var databases = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var dbName = reader.GetString(0);
                if (dbName != "information_schema" && dbName != "mysql" && 
                    dbName != "performance_schema" && dbName != "sys")
                {
                    databases.Add(dbName);
                }
            }

            return Ok(new
            {
                success = true,
                message = "Подключение успешно!",
                databases
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                message = $"Ошибка подключения: {ex.Message}",
                databases = Array.Empty<string>()
            });
        }
    }

    // ── Steam API Key ──────────────────────────────────────────

    /// <summary>
    /// GET /api/settings/steam-api-key
    /// Получить Steam API Key (маскированный).
    /// </summary>
    [HttpGet("steam-api-key")]
    public async Task<IActionResult> GetSteamApiKey()
    {
        var key = await _settings.GetSettingAsync(SettingKeys.SteamApiKey);
        return Ok(new
        {
            isConfigured = !string.IsNullOrWhiteSpace(key),
            maskedKey = string.IsNullOrWhiteSpace(key) 
                ? "" 
                : key[..4] + "••••••••" + key[^4..]
        });
    }

    /// <summary>
    /// PUT /api/settings/steam-api-key
    /// Сохранить Steam API Key.
    /// </summary>
    [HttpPut("steam-api-key")]
    public async Task<IActionResult> SaveSteamApiKey([FromBody] SteamApiKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return BadRequest(new { message = "API Key не может быть пустым" });

        await _settings.SetSettingAsync(SettingKeys.SteamApiKey, request.ApiKey.Trim());
        return Ok(new { message = "Steam API Key сохранён" });
    }
}

// ─── Request DTOs ────────────────────────────────────────────

public record SaveConnectionRequest
{
    public required string Host { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public required List<string> InstanceNames { get; init; }
    public required string SessionName { get; init; }
    public string? Description { get; init; }
}

public record TestConnectionRequest
{
    public required string Host { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public string? TestDatabase { get; init; }
}

public record SteamApiKeyRequest
{
    public required string ApiKey { get; init; }
}
