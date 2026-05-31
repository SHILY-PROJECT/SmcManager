using SmcManager.Core.Interfaces;

namespace SmcManager.Maui.Services;

/// <summary>
/// «Поделиться» с текстом и медиа.
/// </summary>
public sealed class MediaShareService : IMediaShareService
{
    public Task ShareAsync(string? title, string? text, IReadOnlyList<string> filePaths)
    {
        var mediaPaths = filePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => Path.GetFullPath(path))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mediaPaths.Count == 0 && string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

#if ANDROID
        return Platforms.Android.AndroidMediaShareHelper.ShareAsync(title, text, mediaPaths);
#elif WINDOWS
        return Platforms.Windows.WindowsMediaShareHelper.ShareAsync(title, text, mediaPaths);
#else
        return ShareFallbackAsync(title, text, mediaPaths);
#endif
    }

    private static async Task ShareFallbackAsync(string? title, string? text, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = title,
                Text = text!
            });
            return;
        }

        if (paths.Count == 1 && string.IsNullOrWhiteSpace(text))
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(paths[0])
            });
            return;
        }

        if (paths.Count == 1 && !string.IsNullOrWhiteSpace(text))
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = title,
                Text = text
            });
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(paths[0])
            });
            return;
        }

        var files = paths.Select(static path => new ShareFile(path)).ToList();
        await Share.Default.RequestAsync(new ShareMultipleFilesRequest
        {
            Title = title,
            Files = files
        });
    }
}
