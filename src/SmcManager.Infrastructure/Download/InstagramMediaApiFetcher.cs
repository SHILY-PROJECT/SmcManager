using System.Net;
using System.Text.Json;
using SmcManager.Core.Models;
using SmcManager.Core.Services;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Загрузка URL медиа через Instagram API (карусели и фото, когда yt-dlp не справляется).
/// </summary>
internal static class InstagramMediaApiFetcher
{
    public static async Task<IReadOnlyList<string>> TryGetMediaUrlsAsync(
        string postUrl,
        SocialAccount? account,
        bool videosOnly,
        CancellationToken cancellationToken)
    {
        var json = await TryFetchMediaInfoJsonAsync(postUrl, account, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json)
            ? []
            : ExtractUrlsFromApiJson(json, videosOnly);
    }

    public static async Task<string?> TryGetAuthorUsernameAsync(
        string postUrl,
        SocialAccount account,
        CancellationToken cancellationToken)
    {
        var json = await TryFetchMediaInfoJsonAsync(postUrl, account, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json) ? null : ExtractUsernameFromApiJson(json);
    }

    public static async Task<string?> TryGetCaptionAsync(
        string postUrl,
        SocialAccount account,
        CancellationToken cancellationToken)
    {
        var json = await TryFetchMediaInfoJsonAsync(postUrl, account, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json) ? null : ExtractCaptionFromApiJson(json);
    }

    /// <summary>URL превью (фото обложки или первый кадр карусели).</summary>
    public static async Task<string?> TryGetPreviewThumbnailUrlAsync(
        string postUrl,
        SocialAccount? account,
        CancellationToken cancellationToken)
    {
        var json = await TryFetchMediaInfoJsonAsync(postUrl, account, cancellationToken).ConfigureAwait(false);
        var fromApi = ExtractPreviewThumbnailFromApiJson(json);
        if (!string.IsNullOrWhiteSpace(fromApi))
            return fromApi;

        foreach (var fetchUrl in BuildEmbedFetchUrls(postUrl))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var html = await TryFetchHtmlAsync(fetchUrl, account, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(html))
                continue;

            var url = InstagramHtmlMediaExtractor.TryPickPreviewImageUrl(html);
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }

        return await InstagramOEmbedFetcher.TryGetThumbnailUrlAsync(postUrl, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<string?> TryFetchHtmlAsync(
        string url,
        SocialAccount? account,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateHttpClient(account);
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    internal static IEnumerable<string> BuildEmbedFetchUrls(string postUrl)
    {
        var normalized = postUrl.TrimEnd('/');
        yield return normalized.EndsWith("/embed/captioned/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + "/embed/captioned/";
        yield return normalized;
    }

    internal static bool IsVideoMediaUrl(string url) =>
        InstagramHtmlMediaExtractor.IsVideoMediaUrl(url);

    internal static HttpClient CreateHttpClient(SocialAccount? account)
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
        var cookieHeader = account is null ? null : SocialAccountAuth.BuildCookieHeader(account);
        SocialAccountAuth.ApplyInstagramApiHeaders(client.DefaultRequestHeaders, cookieHeader ?? string.Empty);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        return client;
    }

    private static async Task<string?> TryFetchMediaInfoJsonAsync(
        string postUrl,
        SocialAccount? account,
        CancellationToken cancellationToken)
    {
        if (account is null
            || !InstagramUrlParser.TryParse(postUrl, out var shortCode, out _, out _)
            || string.IsNullOrWhiteSpace(shortCode)
            || !InstagramShortcodeConverter.TryToMediaPk(shortCode, out var mediaPk))
        {
            return null;
        }

        try
        {
            using var client = CreateHttpClient(account);
            var apiUrl = $"https://www.instagram.com/api/v1/media/{mediaPk}/info/";
            using var response = await client.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractUsernameFromApiJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
                return null;

            var item = items[0];
            if (item.TryGetProperty("user", out var user)
                && user.TryGetProperty("username", out var usernameProp))
            {
                return usernameProp.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? ExtractCaptionFromApiJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
                return null;

            return ExtractCaptionFromItem(items[0]);
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? ExtractCaptionFromItem(JsonElement item)
    {
        if (item.TryGetProperty("caption", out var caption))
        {
            if (caption.ValueKind == JsonValueKind.Object
                && caption.TryGetProperty("text", out var textProp))
            {
                var text = textProp.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            if (caption.ValueKind == JsonValueKind.String)
            {
                var text = caption.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        if (item.TryGetProperty("caption_text", out var captionText))
        {
            var text = captionText.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractUrlsFromApiJson(string json, bool videosOnly)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
                return [];

            var item = items[0];
            var urls = new List<string>();

            if (item.TryGetProperty("carousel_media", out var carousel) && carousel.ValueKind == JsonValueKind.Array)
            {
                foreach (var slide in carousel.EnumerateArray())
                    AddSlideUrls(slide, urls, videosOnly);
            }
            else
            {
                AddSlideUrls(item, urls, videosOnly);
            }

            return urls
                .Where(InstagramHtmlMediaExtractor.IsPostMediaUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static void AddSlideUrls(JsonElement slide, List<string> urls, bool videosOnly)
    {
        if (slide.TryGetProperty("video_versions", out var videos) && videos.ValueKind == JsonValueKind.Array)
        {
            string? bestUrl = null;
            var bestPixels = -1L;

            foreach (var video in videos.EnumerateArray())
            {
                if (!video.TryGetProperty("url", out var urlProp))
                    continue;

                var url = urlProp.GetString();
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                var width = video.TryGetProperty("width", out var widthProp) ? widthProp.GetInt32() : 0;
                var height = video.TryGetProperty("height", out var heightProp) ? heightProp.GetInt32() : 0;
                var pixels = (long)Math.Max(width, 0) * Math.Max(height, 0);
                if (pixels >= bestPixels)
                {
                    bestPixels = pixels;
                    bestUrl = url;
                }
            }

            TryAddUrl(bestUrl, urls);
        }

        if (videosOnly)
            return;

        if (slide.TryGetProperty("image_versions2", out var images)
            && images.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array)
        {
            string? bestUrl = null;
            var bestScore = -1;

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("url", out var urlProp))
                    continue;

                var url = urlProp.GetString();
                if (string.IsNullOrWhiteSpace(url) || !InstagramHtmlMediaExtractor.IsPostMediaUrl(url))
                    continue;

                var score = InstagramHtmlMediaExtractor.ScoreImageCandidate(candidate, url);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestUrl = url;
            }

            TryAddUrl(bestUrl, urls);
        }
    }

    private static void TryAddUrl(string? url, List<string> urls)
    {
        if (string.IsNullOrWhiteSpace(url) || !InstagramHtmlMediaExtractor.IsPostMediaUrl(url))
            return;

        urls.Add(url.Trim());
    }

    private static string? ExtractPreviewThumbnailFromApiJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
                return null;

            var item = items[0];
            if (item.TryGetProperty("carousel_media", out var carousel) && carousel.ValueKind == JsonValueKind.Array)
            {
                foreach (var slide in carousel.EnumerateArray())
                {
                    var thumb = ExtractPreviewThumbnailFromSlide(slide);
                    if (!string.IsNullOrWhiteSpace(thumb))
                        return thumb;
                }
            }

            return ExtractPreviewThumbnailFromSlide(item);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractPreviewThumbnailFromSlide(JsonElement slide)
    {
        if (slide.TryGetProperty("image_versions2", out var images)
            && images.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array)
        {
            string? bestUrl = null;
            var bestScore = -1;

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("url", out var urlProp))
                    continue;

                var url = urlProp.GetString();
                if (string.IsNullOrWhiteSpace(url) || !InstagramHtmlMediaExtractor.IsPreviewImageUrl(url))
                    continue;

                var score = InstagramHtmlMediaExtractor.ScoreImageCandidate(candidate, url);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestUrl = url;
            }

            if (!string.IsNullOrWhiteSpace(bestUrl))
                return bestUrl;
        }

        foreach (var propertyName in new[] { "thumbnail_url", "display_url", "display_src" })
        {
            if (!slide.TryGetProperty(propertyName, out var prop))
                continue;

            var url = prop.GetString();
            if (!string.IsNullOrWhiteSpace(url) && InstagramHtmlMediaExtractor.IsPreviewImageUrl(url))
                return url.Trim();
        }

        return null;
    }

}
