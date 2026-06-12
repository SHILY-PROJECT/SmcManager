using CommunityToolkit.Mvvm.ComponentModel;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using SmcManager.Maui.Services;

namespace SmcManager.Maui.Models;

/// <summary>
/// Строка аккаунта в настройках.
/// </summary>
public partial class SocialAccountRowViewModel : ObservableObject
{
    public SocialAccountRowViewModel(SocialAccount account)
    {
        Account = account;
        IsActive = account.IsActive;
        PlatformIconFile = SocialPlatformIcons.GetIconFileName(account.Platform);

        var username = ResolveUsernameForDisplay(account);
        var hasCustomTitle = !string.IsNullOrWhiteSpace(account.DisplayName)
                             && !SocialAccountAuth.IsGenericDisplayName(account.DisplayName, account.Platform);

        if (!string.IsNullOrWhiteSpace(username))
        {
            var nick = $"@{username.Trim().TrimStart('@')}";
            if (hasCustomTitle)
            {
                Title = account.DisplayName!.Trim();
                Nickname = nick;
                HasNickname = true;
            }
            else
            {
                Title = nick;
                Nickname = null;
                HasNickname = false;
            }
        }
        else if (hasCustomTitle)
        {
            Title = account.DisplayName!.Trim();
            Nickname = null;
            HasNickname = false;
        }
        else
        {
            Title = SocialAccountAuth.GetPlatformTitle(account.Platform);
            Nickname = null;
            HasNickname = false;
        }

        var platform = SocialAccountAuth.GetPlatformTitle(account.Platform);
        var auth = SocialAccountAuth.GetAuthMethodLabel(account);
        Subtitle = account.IsDefault
            ? $"{platform} · {auth} · по умолчанию"
            : $"{platform} · {auth}";
        IsDefault = account.IsDefault;
    }

    private static string? ResolveUsernameForDisplay(SocialAccount account)
    {
        if (!string.IsNullOrWhiteSpace(account.Username))
            return account.Username;

        if (!SocialAccountAuth.HasAuth(account) || string.IsNullOrWhiteSpace(account.Cookies))
            return null;

        return SocialAccountAuth.TryParseUsernameFromCookies(account.Platform, account.Cookies);
    }

    public SocialAccount Account { get; }

    public string Title { get; }

    public string PlatformIconFile { get; }

    public string? Nickname { get; }

    public bool HasNickname { get; }

    public string Subtitle { get; }

    public bool IsDefault { get; }

    [ObservableProperty]
    private bool _isActive;
}
