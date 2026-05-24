using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Infrastructure.Services;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Загрузчик YouTube через yt-dlp (YoutubeDLSharp).
/// </summary>
public class YouTubeDownloader : IContentDownloader
{
    private readonly YtdlpHostService _ytdlp;
    private readonly IContentRepository _repository;
    private readonly IMediaStorageService _storage;

    public YouTubeDownloader(
        YtdlpHostService ytdlp,
        IContentRepository repository,
        IMediaStorageService storage)
    {
        _ytdlp = ytdlp;
        _repository = repository;
        _storage = storage;
    }

    public SocialPlatform Platform => SocialPlatform.YouTube;

    public bool CanHandle(Uri url) =>
        YouTubeUrlParser.TryParse(url.ToString(), out _);

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        if (!YouTubeUrlParser.TryParse(request.Url, out var videoId))
        {
            return new DownloadResult
            {
                Success = false,
                ErrorMessage = "Не удалось распознать ссылку YouTube."
            };
        }

        return await YtdlpDownloadHelper.DownloadAsync(
            _ytdlp,
            request,
            SocialPlatform.YouTube,
            ContentKind.Post,
            videoId,
            _repository,
            _storage,
            cancellationToken);
    }
}
