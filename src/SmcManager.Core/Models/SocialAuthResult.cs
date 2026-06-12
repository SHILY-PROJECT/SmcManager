using SmcManager.Core.Enums;

namespace SmcManager.Core.Models;

/// <summary>
/// Результат входа через браузер.
/// </summary>
public class SocialAuthResult
{
    public required SocialPlatform Platform { get; init; }

    public required string Cookies { get; init; }

    public string? Username { get; init; }

    public SocialAuthMethod AuthMethod { get; init; } = SocialAuthMethod.WebLogin;

    /// <summary>Сессия уже проверена на экране входа (повторно не проверять).</summary>
    public bool IsSessionValidated { get; init; }
}
