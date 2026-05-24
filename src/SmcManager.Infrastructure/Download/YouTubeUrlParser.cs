using System.Text.RegularExpressions;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Парсинг ссылок YouTube (watch, shorts, youtu.be).
/// </summary>
public static partial class YouTubeUrlParser
{
    public static bool TryParse(string url, out string videoId)
    {
        videoId = string.Empty;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("youtu.be"))
        {
            videoId = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault() ?? string.Empty;
            return IsValidId(videoId);
        }

        if (!host.Contains("youtube.com") && !host.Contains("youtube-nocookie.com"))
            return false;

        videoId = GetQueryParam(uri, "v") ?? string.Empty;
        if (IsValidId(videoId)) return true;

        var path = uri.AbsolutePath.ToLowerInvariant();
        var shorts = ShortsRegex().Match(path);
        if (shorts.Success)
        {
            videoId = shorts.Groups["id"].Value;
            return IsValidId(videoId);
        }

        var embed = EmbedRegex().Match(path);
        if (embed.Success)
        {
            videoId = embed.Groups["id"].Value;
            return IsValidId(videoId);
        }

        return false;
    }

    private static string? GetQueryParam(Uri uri, string name)
    {
        var query = uri.Query.TrimStart('?');
        if (string.IsNullOrEmpty(query)) return null;

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var key = part[..eq];
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) continue;
            var value = part[(eq + 1)..];
            return Uri.UnescapeDataString(value);
        }

        return null;
    }

    private static bool IsValidId(string id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length is >= 6 and <= 20;

    [GeneratedRegex(@"^/shorts/(?<id>[\w-]{6,})", RegexOptions.IgnoreCase)]
    private static partial Regex ShortsRegex();

    [GeneratedRegex(@"^/embed/(?<id>[\w-]{6,})", RegexOptions.IgnoreCase)]
    private static partial Regex EmbedRegex();
}
