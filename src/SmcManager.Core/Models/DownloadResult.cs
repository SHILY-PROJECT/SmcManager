namespace SmcManager.Core.Models;

/// <summary>
/// Результат операции скачивания.
/// </summary>
public class DownloadResult
{
    public bool Success { get; set; }

    public ContentItem? Content { get; set; }

    public string? ErrorMessage { get; set; }
}
