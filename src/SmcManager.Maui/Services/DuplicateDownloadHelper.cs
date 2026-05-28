using CommunityToolkit.Mvvm.Messaging;
using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;
using SmcManager.Maui.Messages;

namespace SmcManager.Maui.Services;

/// <summary>
/// Подтверждение замены уже скачанного контента по той же ссылке.
/// </summary>
public static class DuplicateDownloadHelper
{
    public static async Task<bool> ConfirmReplaceIfExistsAsync(
        IContentRepository repository,
        string normalizedUrl,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetContentBySourceUrlAsync(normalizedUrl, cancellationToken);
        if (existing is null)
            return true;

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
            return false;

        var author = string.IsNullOrWhiteSpace(existing.AuthorUsername)
            ? "этот контент"
            : $"@{existing.AuthorUsername.TrimStart('@')}";

        var message =
            $"Запись {author} уже есть в библиотеке. Заменить? Старые файлы будут удалены.";

        var confirm = await page.DisplayAlertAsync(
            "Контент уже скачан",
            message,
            "Заменить",
            "Отмена");

        if (!confirm)
            return false;

        await repository.DeleteContentAsync(existing.Id, cancellationToken);
        WeakReferenceMessenger.Default.Send(new ContentDeletedMessage());
        return true;
    }
}
