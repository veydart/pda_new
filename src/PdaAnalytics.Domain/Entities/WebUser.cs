namespace PdaAnalytics.Domain.Entities;

/// <summary>
/// Роли пользователей системы.
/// </summary>
public enum UserRole
{
    /// <summary> Полный доступ: управление операторами, настройки сервера, архивация. </summary>
    SuperAdmin,
    
    /// <summary> Аналитик: доступ ко всем данным, но без управления системой. </summary>
    Operator
}

/// <summary>
/// Пользователь веб-интерфейса PDA Analytics.
/// Хранится в PostgreSQL.
/// </summary>
public class WebUser
{
    public int Id { get; set; }
    
    /// <summary> Уникальное имя пользователя (логин). </summary>
    public required string Username { get; set; }
    
    /// <summary> BCrypt-хеш пароля. </summary>
    public required string PasswordHash { get; set; }
    
    /// <summary> Роль пользователя. </summary>
    public UserRole Role { get; set; } = UserRole.Operator;
    
    /// <summary> Дата создания аккаунта. </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary> Активен ли аккаунт. </summary>
    public bool IsActive { get; set; } = true;
}
