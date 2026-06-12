using SmcManager.Core.Interfaces;
using SmcManager.Maui.ViewModels;

namespace SmcManager.Maui.Services;

/// <summary>
/// Подтверждение и удаление скачанного контента.
/// </summary>
public static class ContentDeletionHelper
{
    public static async Task<bool> ConfirmAndDeleteAsync(
        IContentRepository repository,
        ContentItemDisplayModel item,
        CancellationToken cancellationToken = default)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return false;

        var title = string.IsNullOrWhiteSpace(item.AuthorUsername)
            ? "Удалить запись?"
            : $"Удалить запись @{item.AuthorUsername.TrimStart('@')}?";

        var message = item.MediaCount > 1
            ? $"Будут удалены {item.MediaCount} файлов с устройства и из библиотеки."
            : "Файл будет удалён с устройства и из библиотеки.";

        var confirm = await page.DisplayAlertAsync(title, message, "Удалить", "Отмена");
        if (!confirm) return false;

        await repository.DeleteContentAsync(item.Id, cancellationToken);
        return true;
    }
}
