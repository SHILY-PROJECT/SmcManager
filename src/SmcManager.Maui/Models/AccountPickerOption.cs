using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Maui.Models;

/// <summary>
/// Пункт выбора аккаунта на вкладке «Скачать».
/// </summary>
public class AccountPickerOption
{
    public int? AccountId { get; init; }

    public SocialPlatform? Platform { get; init; }

    public string Title { get; init; } = string.Empty;

    /// <summary>Краткая подпись для бейджа предпросмотра.</summary>
    public string ShortTitle { get; init; } = string.Empty;

    public bool IsNoAccount { get; init; }

    public static AccountPickerOption WithoutAccount(SocialPlatform platform) => new()
    {
        Platform = platform,
        Title = "Без аккаунта (без cookies)",
        ShortTitle = "Без аккаунта",
        IsNoAccount = true
    };

    public static AccountPickerOption FromAccount(SocialAccount account, bool isDefault)
    {
        var name = Core.Services.SocialAccountAuth.GetAccountShortLabel(account);

        var suffix = isDefault ? " · по умолчанию" : string.Empty;
        var auth = Core.Services.SocialAccountAuth.GetAuthMethodLabel(account);

        return new AccountPickerOption
        {
            AccountId = account.Id,
            Platform = account.Platform,
            Title = $"{name} ({auth}){suffix}",
            ShortTitle = name ?? string.Empty
        };
    }
}
