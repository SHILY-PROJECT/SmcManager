using SmcManager.Core.Enums;

namespace SmcManager.Maui.Services;

/// <summary>
/// Иконки платформ для UI.
/// </summary>
public static class SocialPlatformIcons
{
    public static string GetIconFileName(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Instagram => "icon_platform_instagram.png",
        SocialPlatform.YouTube => "icon_platform_youtube.png",
        SocialPlatform.Vkontakte => "icon_platform_vk.png",
        _ => "icon_platform_instagram.png"
    };

    public static string GetAuthStatusIconFileName(bool usesAuthenticatedAccount) =>
        usesAuthenticatedAccount ? "icon_user_access.png" : "icon_user_none.png";
}
