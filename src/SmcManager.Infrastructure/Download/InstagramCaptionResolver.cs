using SmcManager.Core.Models;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Текст описания поста Instagram без yt-dlp (Android / прямая загрузка).
/// </summary>
internal static class InstagramCaptionResolver
{
    public static async Task<string?> ResolveAsync(
        string postUrl,
        SocialAccount? account,
        CancellationToken cancellationToken)
    {
        if (account is not null)
        {
            var fromApi = await InstagramMediaApiFetcher.TryGetCaptionAsync(
                postUrl, account, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fromApi))
                return fromApi.Trim();
        }

        foreach (var fetchUrl in BuildFetchUrls(postUrl))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var html = await TryFetchHtmlAsync(fetchUrl, account, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(html))
                continue;

            var fromHtml = InstagramHtmlCaptionExtractor.FromHtml(html);
            if (!string.IsNullOrWhiteSpace(fromHtml))
                return fromHtml.Trim();
        }

        return null;
    }

    private static IEnumerable<string> BuildFetchUrls(string postUrl)
    {
        var normalized = postUrl.TrimEnd('/');
        yield return normalized.EndsWith("/embed/captioned/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + "/embed/captioned/";
        yield return normalized;
    }

    private static async Task<string?> TryFetchHtmlAsync(
        string url,
        SocialAccount? account,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = InstagramMediaApiFetcher.CreateHttpClient(account);
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
}
