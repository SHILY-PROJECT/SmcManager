namespace SmcManager.Core.Services;

/// <summary>
/// Очистка ссылок от utm/igsh и приведение к каноническому виду для yt-dlp.
/// </summary>
public static class ContentUrlNormalizer
{
    /// <summary>
    /// Обрезает текст до первой http(s)-ссылки (Instagram Share может добавлять теги в начало).
    /// </summary>
    public static string ExtractHttpUrl(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var trimmed = text.Trim();
        var start = FindHttpUrlStart(trimmed);
        if (start < 0)
            return trimmed;

        var candidate = trimmed[start..];
        var end = candidate.IndexOfAny([' ', '\t', '\r', '\n']);
        if (end > 0)
            candidate = candidate[..end];

        return candidate.TrimEnd('.', ',', ';', ')', ']', '"', '\'');
    }

    public static string Normalize(string url)
    {
        var extracted = ExtractHttpUrl(url);
        if (!Uri.TryCreate(extracted.Trim(), UriKind.Absolute, out var uri))
            return extracted.Trim();

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

    private static int FindHttpUrlStart(string text)
    {
        var https = text.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
        var http = text.IndexOf("http://", StringComparison.OrdinalIgnoreCase);

        if (https < 0)
            return http;

        if (http < 0)
            return https;

        return Math.Min(https, http);
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
