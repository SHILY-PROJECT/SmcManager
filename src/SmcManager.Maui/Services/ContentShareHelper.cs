using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;

namespace SmcManager.Maui.Services;

/// <summary>
/// Отправка всего скачанного контента (медиа + текст) из списков и детального экрана.
/// </summary>
public static class ContentShareHelper
{
    public static async Task ShareContentAsync(
        IContentRepository repository,
        IMediaShareService mediaShare,
        BottomToastService toast,
        int contentId,
        CancellationToken cancellationToken = default)
    {
        var item = await repository.GetContentByIdAsync(contentId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            await toast.ShowWarningAsync("Контент не найден.").ConfigureAwait(false);
            return;
        }

        await ShareContentAsync(mediaShare, toast, item).ConfigureAwait(false);
    }

    public static async Task ShareContentAsync(
        IMediaShareService mediaShare,
        BottomToastService toast,
        ContentItem item,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var text = BuildShareText(item.Caption, item.UserComment);
        var paths = CollectShareablePaths(item);

        if (paths.Count == 0 && string.IsNullOrWhiteSpace(text))
        {
            await toast.ShowWarningAsync("Нечего отправить.").ConfigureAwait(false);
            return;
        }

        var title = string.IsNullOrWhiteSpace(item.AuthorUsername)
            ? "Контент"
            : $"@{item.AuthorUsername.TrimStart('@')}";

        try
        {
            await mediaShare.ShareAsync(title, text, paths).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await toast.ShowWarningAsync($"Не удалось отправить: {ex.Message}").ConfigureAwait(false);
        }
    }

    public static string? BuildShareText(string? caption, string? userComment)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(caption))
            parts.Add($"Описание:\n{caption.Trim()}");
        if (!string.IsNullOrWhiteSpace(userComment))
            parts.Add($"Комментарий:\n{userComment.Trim()}");

        return parts.Count > 0 ? string.Join("\n\n", parts) : null;
    }

    private static List<string> CollectShareablePaths(ContentItem item)
    {
        var paths = new List<string>();
        foreach (var media in item.MediaFiles)
        {
            if (string.IsNullOrWhiteSpace(media.LocalPath))
                continue;

            var path = Path.GetFullPath(media.LocalPath);
            if (File.Exists(path))
                paths.Add(path);
        }

        return paths;
    }
}
