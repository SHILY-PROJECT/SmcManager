using SmcManager.Core.Enums;

namespace SmcManager.Maui.Models;

/// <summary>
/// Платформа для Picker в настройках.
/// </summary>
public sealed class PlatformOption
{
    public required SocialPlatform Platform { get; init; }

    public required string Title { get; init; }

    public override string ToString() => Title;
}
