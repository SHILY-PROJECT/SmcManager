using SmcManager.Core.Interfaces;
using SmcManager.Core.Models;

namespace SmcManager.Maui.Services;

/// <summary>
/// Создание пользовательских тегов с проверкой имени.
/// </summary>
public sealed class TagCreationService(IContentRepository repository)
{
    public async Task<(bool Success, ContentTag? Tag, string? ErrorMessage)> TryCreateAsync(
        string name,
        string colorHex,
        CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return (false, null, "Введите название тега.");

        if (trimmed.Length > 32)
            return (false, null, "Название не длиннее 32 символов.");

        var existing = await repository.GetTagsAsync(cancellationToken);
        if (existing.Any(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return (false, null, "Тег с таким именем уже есть.");

        var tag = await repository.SaveTagAsync(new ContentTag
        {
            Name = trimmed,
            ColorHex = colorHex
        }, cancellationToken);

        return (true, tag, null);
    }

    public async Task<(bool Success, ContentTag? Tag, string? ErrorMessage)> TryUpdateAsync(
        int tagId,
        string name,
        string colorHex,
        CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return (false, null, "Введите название тега.");

        if (trimmed.Length > 32)
            return (false, null, "Название не длиннее 32 символов.");

        var existing = await repository.GetTagsAsync(cancellationToken);
        var tag = existing.FirstOrDefault(t => t.Id == tagId);
        if (tag is null)
            return (false, null, "Тег не найден.");

        if (existing.Any(t => t.Id != tagId
                              && string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return (false, null, "Тег с таким именем уже есть.");

        tag.Name = trimmed;
        tag.ColorHex = colorHex;
        var saved = await repository.SaveTagAsync(tag, cancellationToken);
        return (true, saved, null);
    }
}
