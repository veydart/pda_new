using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Configuration;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Api.Services;

/// <summary>
/// Сервис аутентификации и управления пользователями.
/// </summary>
public class AuthService
{
    private readonly AnalyticsDbContext _db;
    private readonly JwtSettings _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AnalyticsDbContext db, IOptions<JwtSettings> jwt, ILogger<AuthService> logger)
    {
        _db = db;
        _jwt = jwt.Value;
        _logger = logger;
    }

    /// <summary>
    /// Создаёт SuperAdmin при первом старте (если в БД нет пользователей).
    /// Вызывается из Program.cs.
    /// </summary>
    public async Task SeedSuperAdminAsync(string defaultPassword = "admin")
    {
        if (await _db.WebUsers.AnyAsync())
        {
            _logger.LogDebug("WebUsers уже существуют, seed пропущен");
            return;
        }

        var admin = new WebUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
            Role = UserRole.SuperAdmin,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.WebUsers.Add(admin);
        await _db.SaveChangesAsync();
        _logger.LogInformation("═══ Создан SuperAdmin (логин: admin, пароль: {Password}) ═══", defaultPassword);
    }

    /// <summary>
    /// Аутентификация: проверяет логин/пароль и возвращает JWT-токен.
    /// </summary>
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var user = await _db.WebUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user is null)
            return AuthResult.Fail("Неверное имя пользователя или пароль");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return AuthResult.Fail("Неверное имя пользователя или пароль");

        var token = GenerateJwtToken(user);

        return AuthResult.Success(token, new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
        });
    }

    /// <summary>
    /// Создание нового оператора (только SuperAdmin).
    /// </summary>
    public async Task<AuthResult> CreateOperatorAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            return AuthResult.Fail("Имя пользователя должно быть не менее 3 символов");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            return AuthResult.Fail("Пароль должен быть не менее 4 символов");

        if (await _db.WebUsers.AnyAsync(u => u.Username == username))
            return AuthResult.Fail($"Пользователь '{username}' уже существует");

        var user = new WebUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.Operator,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.WebUsers.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Создан оператор: {Username}", username);
        return AuthResult.Success(null, new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role.ToString()
        });
    }

    /// <summary>
    /// Удаление оператора (только SuperAdmin). Нельзя удалить себя.
    /// </summary>
    public async Task<AuthResult> DeleteUserAsync(int userId, int callerUserId)
    {
        if (userId == callerUserId)
            return AuthResult.Fail("Нельзя удалить самого себя");

        var user = await _db.WebUsers.FindAsync(userId);
        if (user is null)
            return AuthResult.Fail("Пользователь не найден");

        if (user.Role == UserRole.SuperAdmin)
            return AuthResult.Fail("Нельзя удалить SuperAdmin");

        _db.WebUsers.Remove(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Удалён пользователь: {Username} (ID={Id})", user.Username, user.Id);
        return AuthResult.Success(null, null);
    }

    /// <summary>
    /// Смена пароля пользователя (SuperAdmin может менять любому).
    /// </summary>
    public async Task<AuthResult> ChangePasswordAsync(int targetUserId, string newPassword, int callerUserId, UserRole callerRole)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            return AuthResult.Fail("Пароль должен быть не менее 4 символов");

        // Обычный оператор может менять только свой пароль
        if (callerRole != UserRole.SuperAdmin && targetUserId != callerUserId)
            return AuthResult.Fail("Недостаточно прав");

        var user = await _db.WebUsers.FindAsync(targetUserId);
        if (user is null)
            return AuthResult.Fail("Пользователь не найден");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Сменён пароль: {Username} (ID={Id})", user.Username, user.Id);
        return AuthResult.Success(null, null);
    }

    /// <summary>
    /// Список всех пользователей (для админки).
    /// </summary>
    public async Task<List<UserInfo>> GetAllUsersAsync()
    {
        return await _db.WebUsers
            .OrderBy(u => u.Id)
            .Select(u => new UserInfo
            {
                Id = u.Id,
                Username = u.Username,
                Role = u.Role.ToString(),
                CreatedAt = u.CreatedAt,
                IsActive = u.IsActive
            })
            .ToListAsync();
    }

    // ─── JWT ──────────────────────────────────────────────────

    private string GenerateJwtToken(WebUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwt.ExpirationHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// ─── Result Models ──────────────────────────────────────────

public class AuthResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public string? Token { get; init; }
    public UserInfo? User { get; init; }

    public static AuthResult Success(string? token, UserInfo? user) =>
        new() { IsSuccess = true, Token = token, User = user };

    public static AuthResult Fail(string error) =>
        new() { IsSuccess = false, Error = error };
}

public class UserInfo
{
    public int Id { get; init; }
    public required string Username { get; init; }
    public required string Role { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsActive { get; init; }
}
