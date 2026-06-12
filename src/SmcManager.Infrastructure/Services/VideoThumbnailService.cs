using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using SmcManager.Core.Services;

namespace SmcManager.Infrastructure.Services;

/// <summary>
/// Сохраняет thumb.jpg для видео-постов (URL превью или кадр через ffmpeg).
/// </summary>
public sealed class VideoThumbnailService
{
    private readonly ILogger<VideoThumbnailService> _logger;

    public VideoThumbnailService(ILogger<VideoThumbnailService> logger) => _logger = logger;

    public async Task<bool> TrySaveThumbnailAsync(
        string contentDirectory,
        string? remoteThumbnailUrl,
        string? localVideoPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(contentDirectory);
        var thumbPath = ContentThumbnailHelper.GetThumbnailFilePath(contentDirectory);
        if (File.Exists(thumbPath))
        {
            _logger.LogDebug("TrySaveThumbnailAsync: thumb already exists at {Path}", thumbPath);
            return true;
        }

        _logger.LogDebug(
            "TrySaveThumbnailAsync: dir={Dir}, remoteUrl={RemoteUrl}, localVideo={LocalVideo}",
            contentDirectory,
            remoteThumbnailUrl,
            localVideoPath);

        if (!string.IsNullOrWhiteSpace(remoteThumbnailUrl)
            && await TryDownloadThumbnailAsync(remoteThumbnailUrl, thumbPath, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("TrySaveThumbnailAsync: saved from URL {Url}", remoteThumbnailUrl);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(localVideoPath) && File.Exists(localVideoPath))
        {
            var extracted = TryExtractFrame(localVideoPath, thumbPath);
            _logger.LogInformation(
                "TrySaveThumbnailAsync: ffmpeg extract {Result} from {Video}",
                extracted,
                localVideoPath);
            return extracted;
        }

        _logger.LogWarning(
            "TrySaveThumbnailAsync: failed. remoteUrl={RemoteUrl}, localVideo={LocalVideo}",
            remoteThumbnailUrl,
            localVideoPath);
        return false;
    }

    private async Task<bool> TryDownloadThumbnailAsync(
        string url,
        string destination,
        CancellationToken cancellationToken)
    {
        try
        {
            using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Referer", "https://www.instagram.com/");
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = File.Create(destination);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

            var size = new FileInfo(destination).Length;
            var ok = size >= 512;
            if (!ok)
                _logger.LogWarning("TryDownloadThumbnailAsync: file too small ({Size} bytes) from {Url}", size, url);

            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryDownloadThumbnailAsync failed for {Url}", url);
            TryDelete(destination);
            return false;
        }
    }

    private bool TryExtractFrame(string videoPath, string thumbPath)
    {
        var ffmpeg = FfmpegLocator.GetExecutablePath();
        if (ffmpeg is null)
        {
            _logger.LogWarning("TryExtractFrame: ffmpeg not found");
            return false;
        }

        try
        {
            var args =
                $"-y -hide_banner -loglevel error -ss 0.5 -i \"{videoPath}\" -frames:v 1 -q:v 2 \"{thumbPath}\"";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            process.Start();
            process.WaitForExit(TimeSpan.FromMinutes(2));

            var ok = process.ExitCode == 0
                     && File.Exists(thumbPath)
                     && new FileInfo(thumbPath).Length >= 512;

            if (!ok)
                _logger.LogWarning(
                    "TryExtractFrame: exitCode={Code}, exists={Exists}",
                    process.ExitCode,
                    File.Exists(thumbPath));

            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryExtractFrame failed for {Video}", videoPath);
            TryDelete(thumbPath);
            return false;
        }
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
