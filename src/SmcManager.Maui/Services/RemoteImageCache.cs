using System.Security.Cryptography;
using System.Text;

namespace SmcManager.Maui.Services;

/// <summary>
/// Кэширует удалённые превью в AppData — надёжное отображение Image на Android.
/// </summary>
public sealed class RemoteImageCache
{
    private readonly string _cacheDir;

    public RemoteImageCache()
    {
        _cacheDir = Path.Combine(FileSystem.CacheDirectory, "remote-images");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<string?> GetCachedFilePathAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var cachePath = Path.Combine(_cacheDir, BuildCacheFileName(uri));
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length >= 256)
            return cachePath;

        try
        {
            using var handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Referer", "https://www.instagram.com/");
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = File.Create(cachePath);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

            return new FileInfo(cachePath).Length >= 256 ? cachePath : null;
        }
        catch
        {
            TryDelete(cachePath);
            return url;
        }
    }

    public async Task<ImageSource?> GetImageSourceAsync(string? url, CancellationToken cancellationToken = default)
    {
        var path = await GetCachedFilePathAsync(url, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? ImageSource.FromUri(new Uri(path))
            : ImageSource.FromFile(path);
    }

    public static ImageSource? SourceFromPathOrUrl(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
            return null;

        if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri)
                ? ImageSource.FromUri(uri)
                : null;
        }

        return File.Exists(pathOrUrl) ? ImageSource.FromFile(pathOrUrl) : null;
    }

    public static ImageSource? FromLocalPath(string? path) => SourceFromPathOrUrl(path);

    private static string BuildCacheFileName(Uri uri)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.ToString())))[..16];
        var ext = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrEmpty(ext) || ext.Length > 5)
            ext = ".jpg";

        return hash + ext.ToLowerInvariant();
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
}
