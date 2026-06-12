using Microsoft.Extensions.Logging;
using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Infrastructure.Services;

namespace SmcManager.Infrastructure.Download;

/// <summary>
/// Загрузчик Instagram через yt-dlp (YoutubeDLSharp).
/// </summary>
public class InstagramDownloader : IContentDownloader
{
    private readonly YtdlpHostService _ytdlp;
    private readonly ISocialAccountService _accountService;
    private readonly IContentRepository _repository;
    private readonly IMediaStorageService _storage;
    private readonly ILogger<InstagramDownloader> _logger;

    public InstagramDownloader(
        YtdlpHostService ytdlp,
        ISocialAccountService accountService,
        IContentRepository repository,
        IMediaStorageService storage,
        ILogger<InstagramDownloader> logger)
    {
        _ytdlp = ytdlp;
        _accountService = accountService;
        _repository = repository;
        _storage = storage;
        _logger = logger;
    }

    public SocialPlatform Platform => SocialPlatform.Instagram;

    public bool CanHandle(Uri url)
    {
        var host = url.Host.ToLowerInvariant();
        return host.Contains("instagram.com") || host.Contains("instagr.am");
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        if (!InstagramUrlParser.TryParse(request.Url, out var shortCode, out var kind, out _))
        {
            return new DownloadResult
            {
                Success = false,
                ErrorMessage = "Не удалось распознать ссылку Instagram."
            };
        }

        request.ContentKind = kind;

        if (!YtdlpRuntimeSupport.IsAvailable)
        {
            _logger.LogInformation(
                "Instagram download via direct API (no yt-dlp). shortCode={ShortCode}, kind={Kind}",
                shortCode,
                kind);
            return await InstagramDirectDownloadHelper.DownloadAsync(
                request,
                kind,
                shortCode,
                _accountService,
                _repository,
                _storage,
                _logger,
                cancellationToken);
        }

        _logger.LogInformation("Instagram download via yt-dlp. shortCode={ShortCode}", shortCode);
        return await YtdlpDownloadHelper.DownloadAsync(
            _ytdlp,
            request,
            SocialPlatform.Instagram,
            kind,
            shortCode,
            _repository,
            _storage,
            cancellationToken);
    }
}
