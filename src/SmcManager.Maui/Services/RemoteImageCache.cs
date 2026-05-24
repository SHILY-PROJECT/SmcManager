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

    public async Task<ImageSource?> GetImageSourceAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var cachePath = Path.Combine(_cacheDir, BuildCacheFileName(uri));
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length >= 256)
            return ImageSource.FromFile(cachePath);

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

            return new FileInfo(cachePath).Length >= 256
                ? ImageSource.FromFile(cachePath)
                : null;
        }
        catch
        {
            TryDelete(cachePath);
            return ImageSource.FromUri(uri);
        }
    }

    public static ImageSource? FromLocalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        return ImageSource.FromFile(path);
    }

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
