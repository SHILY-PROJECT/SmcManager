namespace SmcManager.Core.Enums;

/// <summary>
/// Способ подключения аккаунта.
/// </summary>
public enum SocialAuthMethod
{
    /// <summary>Cookies вставлены вручную.</summary>
    ManualCookies = 0,

    /// <summary>Вход через встроенный браузер, cookies извлечены автоматически.</summary>
    WebLogin = 1
}
