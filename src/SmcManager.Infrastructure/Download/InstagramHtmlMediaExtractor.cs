using System.Text.Json;
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
        @"https://[^\s""'<>\\]*(?:cdninstagram\.com|fbcdn\.net)[^\s""'<>\\]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InstagramCdnUrlRegex();

    [GeneratedRegex(
        @"""(?:display_url|video_url|thumbnail_src|thumbnail_url|image_url|preview_url|src)""\s*:\s*""(?<url>https://[^""\\]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonMediaFieldRegex();

    [GeneratedRegex(
        @"property=""og:image""\s+content=""(?<url>https://[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgImagePropertyFirstRegex();

    [GeneratedRegex(
        @"content=""(?<url>https://[^""]+)""\s+property=""og:image""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgImageContentFirstRegex();

    [GeneratedRegex(@"(?<id>\d+_\d+_n)\.(?<ext>jpg|jpeg|webp|mp4)", RegexOptions.IgnoreCase)]
    private static partial Regex MediaIdRegex();

    [GeneratedRegex(@"(?<=[/_-])(?<dim>[sp]\d+x\d+|\d+x\d+)(?=[/_-])", RegexOptions.IgnoreCase)]
    private static partial Regex EmbeddedDimensionRegex();

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

        foreach (Match match in InstagramCdnUrlRegex().Matches(html))
        {
            var url = DecodeHtmlUrl(match.Value);
            TryAddCandidate(candidates, url, ref order);
        }

        foreach (var url in ExtractOgImageUrls(html))
            TryAddCandidate(candidates, url, ref order);

        return SelectBestDistinctUrls(candidates.Values.Select(x => x.Url));
    }

    /// <summary>Лучший URL картинки для превью (фото-посты, обложка reel).</summary>
    public static string? TryPickPreviewImageUrl(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in FromHtml(html))
            candidates.Add(url);

        foreach (var url in ExtractOgImageUrls(html))
            candidates.Add(url);

        var best = candidates
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Where(IsPreviewImageUrl)
            .OrderByDescending(ScoreMediaUrl)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(best))
            return best;

        return ExtractOgImageUrls(html)
            .Select(DecodeHtmlUrl)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u) && IsPreviewImageUrl(u));
    }

    public static string? PickPreviewImageFromVideoData(VideoData? video)
    {
        if (video is null)
            return null;

        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectFromNode(video, urls);

        return urls
            .Where(u => !IsVideoMediaUrl(u))
            .Where(IsPreviewImageUrl)
            .OrderByDescending(ScoreMediaUrl)
            .FirstOrDefault();
    }

    internal static bool IsPreviewImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || IsVideoMediaUrl(url))
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

        return url.Contains("/v/", StringComparison.OrdinalIgnoreCase)
               || url.Contains("stp=dst-", StringComparison.OrdinalIgnoreCase)
               || HasMediaExtension(url);
    }

    internal static bool IsVideoMediaUrl(string url) =>
        url.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
        || url.Contains("stp=dst-mp4", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> SelectBestDistinctUrls(IEnumerable<string> urls)
    {
        var candidates = new Dictionary<string, (string Url, int Score, int Order)>(StringComparer.OrdinalIgnoreCase);
        var order = 0;

        foreach (var url in urls)
            TryAddCandidate(candidates, url, ref order);

        return candidates.Values
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Order)
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
            || url.Contains("s320x320", StringComparison.OrdinalIgnoreCase)
            || url.Contains("s640x640", StringComparison.OrdinalIgnoreCase)
            || url.Contains("p640x640", StringComparison.OrdinalIgnoreCase))
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
            || url.Contains(".png", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".heic", StringComparison.OrdinalIgnoreCase))
            return true;

        return url.Contains("stp=dst-jpg", StringComparison.OrdinalIgnoreCase)
               || url.Contains("stp=dst-jpeg", StringComparison.OrdinalIgnoreCase)
               || url.Contains("stp=dst-webp", StringComparison.OrdinalIgnoreCase)
               || url.Contains("stp=dst-heic", StringComparison.OrdinalIgnoreCase)
               || url.Contains("stp=dst-mp4", StringComparison.OrdinalIgnoreCase)
               || url.Contains("/v/t51.", StringComparison.OrdinalIgnoreCase)
               || url.Contains("/v/t39.", StringComparison.OrdinalIgnoreCase)
               || url.Contains("/v/t38.", StringComparison.OrdinalIgnoreCase)
               || url.Contains("/v/t", StringComparison.OrdinalIgnoreCase);
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

        score += ScoreDimensionsFromUrl(url);

        if (url.Contains("/v/t51.", StringComparison.OrdinalIgnoreCase)) score += 150;
        if (url.Contains("/v/t39.", StringComparison.OrdinalIgnoreCase)) score += 80;
        if (!url.Contains("s640x640", StringComparison.OrdinalIgnoreCase)) score += 80;
        if (!url.Contains("p640x640", StringComparison.OrdinalIgnoreCase)) score += 80;
        if (!url.Contains("s150x150", StringComparison.OrdinalIgnoreCase)) score += 40;
        score += Math.Min(url.Length / 20, 100);
        return score;
    }

    internal static int ScoreImageCandidate(JsonElement candidate, string url)
    {
        var width = candidate.TryGetProperty("width", out var widthProp) ? widthProp.GetInt32() : 0;
        var height = candidate.TryGetProperty("height", out var heightProp) ? heightProp.GetInt32() : 0;
        var pixels = (long)Math.Max(width, 0) * Math.Max(height, 0);
        if (pixels > 0)
            return (int)Math.Min(pixels / 100, 2_000_000);

        return ScoreMediaUrl(url);
    }

    private static int ScoreDimensionsFromUrl(string url)
    {
        var bestArea = 0;

        foreach (Match match in EmbeddedDimensionRegex().Matches(url))
        {
            var dim = match.Groups["dim"].Value;
            var parts = dim.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !int.TryParse(parts[0].TrimStart('s', 'S', 'p', 'P'), out var width)
                || !int.TryParse(parts[1], out var height))
                continue;

            bestArea = Math.Max(bestArea, width * height);
        }

        if (bestArea <= 0)
            return 0;

        return Math.Min(bestArea / 100, 2_000_000);
    }

    private static IEnumerable<string> ExtractOgImageUrls(string html)
    {
        foreach (var regex in new[] { OgImagePropertyFirstRegex(), OgImageContentFirstRegex() })
        {
            foreach (Match match in regex.Matches(html))
            {
                var url = DecodeHtmlUrl(match.Groups["url"].Value);
                if (!string.IsNullOrWhiteSpace(url))
                    yield return url;
            }
        }
    }

    private static string DecodeHtmlUrl(string url) =>
        url.Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("\\u0026", "&", StringComparison.Ordinal)
            .Replace("\\/", "/", StringComparison.Ordinal);
}
