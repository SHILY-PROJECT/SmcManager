using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using SmcManager.Core.Services;

namespace SmcManager.Maui.Services;

/// <summary>
/// Параметры HTTP-запроса при загрузке превью (cookies Instagram и т.д.).
/// </summary>
public sealed class RemoteImageRequestOptions
{
    public string? CookieHeader { get; init; }

    public bool UseInstagramHeaders { get; init; }

    public static RemoteImageRequestOptions ForInstagram(string? cookieHeader = null) =>
        new() { CookieHeader = cookieHeader, UseInstagramHeaders = true };
}

/// <summary>
/// Кэширует удалённые превью в AppData — надёжное отображение Image на Android.
/// </summary>
public sealed class RemoteImageCache
{
    private const int MinCachedBytes = 256;

    private readonly string _cacheDir;

    public RemoteImageCache()
    {
        _cacheDir = Path.Combine(FileSystem.CacheDirectory, "remote-images");
        Directory.CreateDirectory(_cacheDir);
    }

    public Task<string?> GetCachedFilePathAsync(string? url, CancellationToken cancellationToken = default) =>
        GetCachedFilePathAsync(url, options: null, cancellationToken);

    public async Task<string?> GetCachedFilePathAsync(
        string? url,
        RemoteImageRequestOptions? options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var cachePath = Path.Combine(_cacheDir, BuildCacheFileName(uri));
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length >= MinCachedBytes)
            return cachePath;

        try
        {
            using var handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplyRequestHeaders(request.Headers, uri, options);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = File.Create(cachePath);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

            return new FileInfo(cachePath).Length >= MinCachedBytes ? cachePath : null;
        }
        catch
        {
            TryDelete(cachePath);
            return null;
        }
    }

    public Task<ImageSource?> GetImageSourceAsync(string? url, CancellationToken cancellationToken = default) =>
        GetImageSourceAsync(url, options: null, cancellationToken);

    public async Task<ImageSource?> GetImageSourceAsync(
        string? url,
        RemoteImageRequestOptions? options,
        CancellationToken cancellationToken = default)
    {
        var path = await GetCachedFilePathAsync(url, options, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(path) ? null : ImageSource.FromFile(path);
    }

    public static ImageSource? SourceFromPathOrUrl(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
            return null;

        if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(pathOrUrl) ? ImageSource.FromFile(pathOrUrl) : null;
    }

    public static ImageSource? FromLocalPath(string? path) => SourceFromPathOrUrl(path);

    private static void ApplyRequestHeaders(
        HttpRequestHeaders headers,
        Uri uri,
        RemoteImageRequestOptions? options)
    {
        if (options?.UseInstagramHeaders == true || IsInstagramMediaUri(uri))
        {
            SocialAccountAuth.ApplyInstagramApiHeaders(headers, options?.CookieHeader ?? string.Empty);
            return;
        }

        headers.TryAddWithoutValidation("Referer", "https://www.instagram.com/");
        headers.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36");
    }

    private static bool IsInstagramMediaUri(Uri uri)
    {
        var host = uri.Host;
        return host.Contains("cdninstagram.com", StringComparison.OrdinalIgnoreCase)
               || host.Contains("fbcdn.net", StringComparison.OrdinalIgnoreCase)
               || host.Contains("instagram.com", StringComparison.OrdinalIgnoreCase);
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
