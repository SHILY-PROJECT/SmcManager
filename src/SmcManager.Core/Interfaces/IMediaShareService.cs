namespace SmcManager.Core.Interfaces;

/// <summary>
/// Отправка медиафайлов с текстовым описанием в системное меню «Поделиться».
/// </summary>
public interface IMediaShareService
{
    Task ShareAsync(string? title, string? text, IReadOnlyList<string> filePaths);
}
