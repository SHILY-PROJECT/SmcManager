using System.Text.RegularExpressions;
using YoutubeDLSharp.Metadata;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Извлечение прямых URL медиа Instagram из HTML страницы и метаданных yt-dlp.
/// </summary>
internal static partial class InstagramHtmlMediaExtractor
{
    [GeneratedRegex(@"https://(?:scontent|lookaside)[^\s""'<>\\]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CdnUrlRegex();

    [GeneratedRegex(
        @"""(?:display_url|video_url|thumbnail_src|src)""\s*:\s*""(?<url>https://[^""\\]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonMediaFieldRegex();

    [GeneratedRegex(@"(?<id>\d+_\d+_n)\.(?<ext>jpg|jpeg|webp|mp4)", RegexOptions.IgnoreCase)]
    private static partial Regex MediaIdRegex();

    public static IReadOnlyList<string> FromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];

        var candidates = new Dictionary<string, (string Url, int Score, int Order)>(StringComparer.OrdinalIgnoreCase);
        var order = 0;

        foreach (Match match in JsonMediaFieldRegex().Matches(html))
        {
            var url = DecodeHtmlUrl(match.Groups["url"].Value);
            TryAddCandidate(candidates, url, ref order);
        }

        foreach (Match match in CdnUrlRegex().Matches(html))
        {
            var url = DecodeHtmlUrl(match.Value);
            TryAddCandidate(candidates, url, ref order);
        }

        return candidates.Values
            .OrderBy(x => x.Order)
            .ThenByDescending(x => x.Score)
            .Select(x => x.Url)
            .ToList();
    }

    public static IReadOnlyList<string> FromVideoData(VideoData? video)
    {
        if (video is null) return [];

        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectFromNode(video, urls);

        return urls
            .Where(IsPostMediaUrl)
            .OrderByDescending(ScoreMediaUrl)
            .ToList();
    }

    private static void TryAddCandidate(
        Dictionary<string, (string Url, int Score, int Order)> candidates,
        string url,
        ref int order)
    {
        if (!IsPostMediaUrl(url)) return;

        var key = GetMediaKey(url);
        var score = ScoreMediaUrl(url);

        if (!candidates.TryGetValue(key, out var existing))
        {
            candidates[key] = (url, score, order++);
            return;
        }

        if (score > existing.Score)
            candidates[key] = (url, score, existing.Order);
    }

    private static void CollectFromNode(VideoData node, HashSet<string> urls)
    {
        if (!string.IsNullOrWhiteSpace(node.Url))
            urls.Add(node.Url.Trim());

        if (!string.IsNullOrWhiteSpace(node.Thumbnail))
            urls.Add(node.Thumbnail.Trim());

        if (node.Thumbnails is { Length: > 0 })
        {
            foreach (var thumb in node.Thumbnails)
            {
                if (!string.IsNullOrWhiteSpace(thumb.Url))
                    urls.Add(thumb.Url.Trim());
            }
        }

        if (node.Formats is { Length: > 0 })
        {
            foreach (var format in node.Formats)
            {
                if (!string.IsNullOrWhiteSpace(format.Url))
                    urls.Add(format.Url.Trim());
            }
        }

        if (node.Entries is not { Length: > 0 }) return;

        foreach (var entry in node.Entries)
            CollectFromNode(entry, urls);
    }

    internal static bool IsPostMediaUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!IsInstagramCdnHost(url))
            return false;

        if (url.Contains("static.cdninstagram.com/rsrc.php", StringComparison.OrdinalIgnoreCase))
            return false;

        if (url.Contains("profile_pic", StringComparison.OrdinalIgnoreCase))
            return false;

        if (url.Contains("s150x150", StringComparison.OrdinalIgnoreCase)
            || url.Contains("s320x320", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!HasMediaExtension(url))
            return false;

        return url.Contains("/v/t", StringComparison.OrdinalIgnoreCase)
               || url.Contains("/v/t51.", StringComparison.OrdinalIgnoreCase)
               || url.Contains("/v/t39.", StringComparison.OrdinalIgnoreCase)
               || url.Contains("stp=dst-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInstagramCdnHost(string url) =>
        url.Contains("cdninstagram.com", StringComparison.OrdinalIgnoreCase)
        || url.Contains("fbcdn.net", StringComparison.OrdinalIgnoreCase)
        || url.Contains("lookaside.instagram.com", StringComparison.OrdinalIgnoreCase);

    private static bool HasMediaExtension(string url)
    {
        if (url.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".jpg", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".webp", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".png", StringComparison.OrdinalIgnoreCase))
            return true;

        return url.Contains("stp=dst-jpg", StringComparison.OrdinalIgnoreCase)
               || url.Contains("stp=dst-webp", StringComparison.OrdinalIgnoreCase)
               || url.Contains("stp=dst-mp4", StringComparison.OrdinalIgnoreCase)
               || url.Contains("/v/t51.", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMediaKey(string url)
    {
        if (TryGetMediaId(url, out var mediaId))
            return mediaId;

        var path = url.Split('?', 2)[0];
        return path;
    }

    private static bool TryGetMediaId(string url, out string mediaId)
    {
        var match = MediaIdRegex().Match(url);
        if (!match.Success)
        {
            mediaId = string.Empty;
            return false;
        }

        mediaId = match.Groups["id"].Value + "." + match.Groups["ext"].Value;
        return true;
    }

    internal static int ScoreMediaUrl(string url)
    {
        var score = 0;
        if (url.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
            || url.Contains("stp=dst-mp4", StringComparison.OrdinalIgnoreCase))
            score += 500;
        if (url.Contains("p1080x1080", StringComparison.OrdinalIgnoreCase)) score += 300;
        if (url.Contains("1080", StringComparison.Ordinal)) score += 200;
        if (url.Contains("1440", StringComparison.Ordinal)) score += 250;
        if (url.Contains("/v/t51.", StringComparison.OrdinalIgnoreCase)) score += 150;
        if (url.Contains("/v/t39.", StringComparison.OrdinalIgnoreCase)) score += 80;
        if (!url.Contains("s640x640", StringComparison.OrdinalIgnoreCase)) score += 80;
        if (!url.Contains("s150x150", StringComparison.OrdinalIgnoreCase)) score += 40;
        score += Math.Min(url.Length / 20, 100);
        return score;
    }

    private static string DecodeHtmlUrl(string url) =>
        url.Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("\\u0026", "&", StringComparison.Ordinal)
            .Replace("\\/", "/", StringComparison.Ordinal);
}
