using SmcManager.Core.Enums;

namespace SmcManager.Core.Models;

/// <summary>
/// Подключённый аккаунт соцсети (логин, сессия, платформа).
/// </summary>
public class SocialAccount
{
    public int Id { get; set; }

    public SocialPlatform Platform { get; set; }

    /// <summary>Подпись в списке (например «Мой Instagram»).</summary>
    public string? DisplayName { get; set; }

    public string Username { get; set; } = string.Empty;

    /// <summary>Cookies или sessionid для авторизованных запросов.</summary>
    public string? Cookies { get; set; }

    /// <summary>Способ подключения: браузер или ручные cookies.</summary>
    public SocialAuthMethod AuthMethod { get; set; } = SocialAuthMethod.ManualCookies;

    /// <summary>Устаревшее поле — при чтении используется <see cref="Cookies"/>.</summary>
    public string? SessionToken { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
}
