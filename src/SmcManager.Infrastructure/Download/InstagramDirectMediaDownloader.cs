using System.Net;
using SmcManager.Core.Enums;
using SmcManager.Core.Models;
using SmcManager.Core.Services;
using YoutubeDLSharp.Metadata;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Скачивание фото/карусели Instagram по прямым URL из embed-страницы (yt-dlp часто не справляется).
/// </summary>
internal static class InstagramDirectMediaDownloader
{
    public static async Task<IReadOnlyList<string>> TryDownloadAsync(
        string postUrl,
        SocialAccount? account,
        string outputDir,
        VideoData? metadata,
        ContentKind? contentKind,
        CancellationToken cancellationToken)
    {
        try
        {
            var videosOnly = contentKind is ContentKind.Reel;
            var mediaUrls = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddUrls(IEnumerable<string> urls)
            {
                foreach (var url in urls)
                {
                    if (videosOnly && !IsVideoUrl(url))
                        continue;

                    if (seen.Add(url))
                        mediaUrls.Add(url);
                }
            }

            AddUrls(await InstagramMediaApiFetcher.TryGetMediaUrlsAsync(
                postUrl, account, videosOnly, cancellationToken).ConfigureAwait(false));

            foreach (var url in InstagramHtmlMediaExtractor.FromVideoData(metadata))
                AddUrls([url]);

            if (!videosOnly)
            {
                foreach (var fetchUrl in BuildFetchUrls(postUrl))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var html = await TryFetchHtmlAsync(fetchUrl, account, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(html)) continue;

                    AddUrls(InstagramHtmlMediaExtractor.FromHtml(html));
                }
            }

            if (mediaUrls.Count == 0)
                return [];

            mediaUrls = InstagramHtmlMediaExtractor.SelectBestDistinctUrls(mediaUrls).ToList();

            Directory.CreateDirectory(outputDir);

            using var client = InstagramMediaApiFetcher.CreateHttpClient(account);
            var saved = new List<string>();
            var index = 0;

            foreach (var mediaUrl in mediaUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                index++;
                var ext = GuessExtension(mediaUrl);
                var path = Path.Combine(outputDir, $"{index:D2}{ext}");

                try
                {
                    var isVideo = ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                                  || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase);
                    await DownloadFileAsync(client, mediaUrl, path, isVideo, cancellationToken)
                        .ConfigureAwait(false);

                    if (MediaFileValidator.IsValidFile(path, requireVideo: videosOnly || isVideo))
                        saved.Add(path);
                    else
                        TryDelete(path);
                }
                catch
                {
                    TryDelete(path);
                }
            }

            return saved;
        }
        catch
        {
            return [];
        }
    }

    private static bool IsVideoUrl(string url) =>
        url.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
        || url.Contains("stp=dst-mp4", StringComparison.OrdinalIgnoreCase)
        || url.Contains("video", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> BuildFetchUrls(string postUrl)
    {
        var normalized = postUrl.TrimEnd('/');
        yield return ToEmbedUrl(normalized);
        yield return normalized;
    }

    private static string ToEmbedUrl(string postUrl) =>
        postUrl.EndsWith("/embed/captioned/", StringComparison.OrdinalIgnoreCase)
            ? postUrl
            : postUrl + "/embed/captioned/";

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

    private static async Task DownloadFileAsync(
        HttpClient client,
        string url,
        string destination,
        bool isVideo,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Referer", "https://www.instagram.com/");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", isVideo ? "video" : "image");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "no-cors");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(destination);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private static string GuessExtension(string url)
    {
        if (url.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
            || url.Contains("stp=dst-mp4", StringComparison.OrdinalIgnoreCase))
            return ".mp4";

        if (url.Contains(".webp", StringComparison.OrdinalIgnoreCase)
            || url.Contains("stp=dst-webp", StringComparison.OrdinalIgnoreCase))
            return ".webp";

        var path = url.Split('?', 2)[0];
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext) || ext.Length > 5)
            return ".jpg";

        return ext.ToLowerInvariant() switch
        {
            ".jpeg" => ".jpg",
            ".mp4" => ".mp4",
            ".webp" => ".webp",
            _ => ext
        };
    }
}
