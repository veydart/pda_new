using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdaAnalytics.Api.Services;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC ENDPOINTS (без авторизации)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/auth/login
    /// Аутентификация. Возвращает JWT-токен.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _auth.LoginAsync(request.Username, request.Password);

        if (!result.IsSuccess)
            return Unauthorized(new { message = result.Error });

        return Ok(new
        {
            token = result.Token,
            user = result.User
        });
    }

    /// <summary>
    /// GET /api/auth/me
    /// Информация о текущем пользователе (по токену).
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = GetUserId(),
            username = User.Identity?.Name,
            role = User.FindFirst(ClaimTypes.Role)?.Value
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  SUPERADMIN ENDPOINTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// GET /api/auth/users
    /// Список всех пользователей (только SuperAdmin).
    /// </summary>
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _auth.GetAllUsersAsync();
        return Ok(users);
    }

    /// <summary>
    /// POST /api/auth/users
    /// Создание нового оператора (только SuperAdmin).
    /// </summary>
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await _auth.CreateOperatorAsync(request.Username, request.Password);

        if (!result.IsSuccess)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = $"Оператор '{request.Username}' создан", user = result.User });
    }

    /// <summary>
    /// DELETE /api/auth/users/{id}
    /// Удаление пользователя (только SuperAdmin).
    /// </summary>
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var result = await _auth.DeleteUserAsync(id, GetUserId());

        if (!result.IsSuccess)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Пользователь удалён" });
    }

    /// <summary>
    /// PUT /api/auth/users/{id}/password
    /// Смена пароля (SuperAdmin — любому, Operator — только себе).
    /// </summary>
    [Authorize]
    [HttpPut("users/{id:int}/password")]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        var callerRole = Enum.Parse<UserRole>(User.FindFirst(ClaimTypes.Role)!.Value);
        var result = await _auth.ChangePasswordAsync(id, request.NewPassword, GetUserId(), callerRole);

        if (!result.IsSuccess)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Пароль изменён" });
    }

    // ─── Helpers ──────────────────────────────────────────────

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}

// ─── Request DTOs ────────────────────────────────────────────

public record LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public record CreateUserRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public record ChangePasswordRequest
{
    public required string NewPassword { get; init; }
}
