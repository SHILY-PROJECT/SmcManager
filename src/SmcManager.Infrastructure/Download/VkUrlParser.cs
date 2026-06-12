using System.Text.RegularExpressions;
using SmcManager.Core.Enums;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Парсинг ссылок ВКонтакте (стена, видео, клипы).
/// </summary>
public static partial class VkUrlParser
{
    public static bool TryParse(string url, out string ownerId, out string itemId, out ContentKind kind)
    {
        ownerId = string.Empty;
        itemId = string.Empty;
        kind = ContentKind.Post;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (!host.Contains("vk.com") && !host.Contains("vkontakte") && !host.Contains("vk.ru"))
            return false;

        var haystack = uri.AbsolutePath + uri.Query;

        var wParam = WParamRegex().Match(haystack);
        if (wParam.Success)
            haystack = wParam.Groups["ref"].Value;

        var match = ContentRegex().Match(haystack);
        if (!match.Success) return false;

        ownerId = match.Groups["owner"].Value;
        itemId = match.Groups["id"].Value;
        var type = match.Groups["type"].Value.ToLowerInvariant();
        kind = type == "clip" ? ContentKind.Reel : ContentKind.Post;
        return true;
    }

    public static string BuildShortCode(string ownerId, string itemId) => $"{ownerId}_{itemId}";

    [GeneratedRegex(@"[?&]w=(?<ref>[^&]+)", RegexOptions.IgnoreCase)]
    private static partial Regex WParamRegex();

    [GeneratedRegex(@"(?<type>wall|video|clip)(?<owner>-?\d+)_(?<id>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ContentRegex();
}
