using SmcManager.Core.Enums;

namespace SmcManager.Core.Services;

/// <summary>
/// URL входа и домены cookies для каждой платформы.
/// </summary>
public static class SocialLoginConfig
{
    public static Uri GetLoginUrl(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Instagram => new Uri("https://www.instagram.com/accounts/login/"),
        SocialPlatform.YouTube => new Uri(
            "https://accounts.google.com/ServiceLogin?service=youtube&passive=true&continue=https%3A%2F%2Fwww.youtube.com%2F"),
        SocialPlatform.Vkontakte => new Uri("https://id.vk.com/auth"),
        _ => new Uri("https://www.google.com")
    };

    /// <summary>Базовые URL для чтения cookies (по одному на домен).</summary>
    public static IReadOnlyList<string> GetCookieUrls(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Instagram =>
        [
            "https://www.instagram.com/",
            "https://instagram.com/"
        ],
        SocialPlatform.YouTube =>
        [
            "https://www.youtube.com/",
            "https://youtube.com/",
            "https://accounts.google.com/"
        ],
        SocialPlatform.Vkontakte =>
        [
            "https://vk.com/",
            "https://m.vk.com/"
        ],
        _ => ["https://www.google.com/"]
    };
}
