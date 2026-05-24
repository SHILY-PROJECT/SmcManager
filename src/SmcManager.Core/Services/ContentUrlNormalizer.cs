namespace SmcManager.Core.Services;

/// <summary>
/// Очистка ссылок от utm/igsh и приведение к каноническому виду для yt-dlp.
/// </summary>
public static class ContentUrlNormalizer
{
    public static string Normalize(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return url.Trim();

        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("instagram.com") || host.Contains("instagr.am"))
        {
            var path = uri.AbsolutePath.TrimEnd('/');
            if (path.Length == 0) path = "/";
            return $"https://www.instagram.com{path}/";
        }

        if (host.Contains("youtu.be"))
            return uri.GetLeftPart(UriPartial.Path);

        if (host.Contains("youtube.com"))
        {
            var videoId = GetQueryParam(uri, "v");
            if (!string.IsNullOrEmpty(videoId)
                && uri.AbsolutePath.Contains("/watch", StringComparison.OrdinalIgnoreCase))
                return $"https://www.youtube.com/watch?v={videoId}";

            return uri.GetLeftPart(UriPartial.Path);
        }

        if (host.Contains("vk.com") || host.Contains("vkontakte"))
            return uri.GetLeftPart(UriPartial.Path);

        return uri.GetLeftPart(UriPartial.Path);
    }

    private static string? GetQueryParam(Uri uri, string name)
    {
        if (string.IsNullOrEmpty(uri.Query)) return null;

        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;

            var key = part[..eq];
            if (!key.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;

            return Uri.UnescapeDataString(part[(eq + 1)..]);
        }

        return null;
    }
}
