using System.Net.Http.Headers;
using System.Text.Json;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Публичное превью Instagram через oEmbed (работает для фото-постов без cookies).
/// </summary>
internal static class InstagramOEmbedFetcher
{
    public static async Task<string?> TryGetThumbnailUrlAsync(
        string postUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalized = postUrl.Trim().TrimEnd('/');
            if (!normalized.EndsWith('/'))
                normalized += '/';

            var requestUrl = "https://api.instagram.com/oembed/?url="
                             + Uri.EscapeDataString(normalized);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

            using var response = await client.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("thumbnail_url", out var thumbProp))
                return null;

            var url = thumbProp.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch
        {
            return null;
        }
    }
}
