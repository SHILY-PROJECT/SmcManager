using SmcManager.Core.Enums;

namespace SmcManager.Maui.Models;

/// <summary>
/// Пункт выбора расположения данных в настройках.
/// </summary>
public sealed class StorageLocationOption
{
    public AppStorageLocation Location { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
