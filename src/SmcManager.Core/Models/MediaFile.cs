using SmcManager.Core.Enums;

namespace SmcManager.Core.Models;

/// <summary>
/// Один медиаресурс внутри поста (карусель может содержать несколько).
/// </summary>
public class MediaFile
{
    public int Id { get; set; }

    public int ContentItemId { get; set; }

    public MediaType MediaType { get; set; }

    /// <summary>Локальный путь к сохранённому файлу.</summary>
    public string LocalPath { get; set; } = string.Empty;

    public string? RemoteUrl { get; set; }

    public int OrderIndex { get; set; }
}
