namespace PdaAnalytics.Domain.Configuration;

/// <summary>
/// Настройки JWT-аутентификации.
/// </summary>
public class JwtSettings
{
    /// <summary> Секретный ключ для подписи токенов (минимум 32 символа). </summary>
    public required string Secret { get; set; }
    
    /// <summary> Издатель токена. </summary>
    public string Issuer { get; set; } = "PdaAnalytics";
    
    /// <summary> Аудитория токена. </summary>
    public string Audience { get; set; } = "PdaAnalytics.Client";
    
    /// <summary> Время жизни токена в часах. </summary>
    public int ExpirationHours { get; set; } = 24;
}
