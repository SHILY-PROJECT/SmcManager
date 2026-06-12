using SmcManager.Core.Enums;
using SmcManager.Core.Models;

namespace SmcManager.Maui.Services;

/// <summary>
/// Контекст модального входа (передаётся на страницу логина).
/// </summary>
public sealed class SocialLoginNavigationContext
{
    public static SocialLoginNavigationContext? Current { get; set; }

    public required SocialPlatform Platform { get; init; }

    public required TaskCompletionSource<SocialAuthResult?> Completion { get; init; }

    public bool IsFinished { get; set; }
}
