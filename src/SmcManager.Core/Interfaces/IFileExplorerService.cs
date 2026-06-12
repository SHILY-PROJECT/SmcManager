namespace SmcManager.Core.Interfaces;

/// <summary>
/// Открытие скачанных файлов и папок в системном проводнике / файловом менеджере.
/// </summary>
public interface IFileExplorerService
{
    /// <summary>Выделить файл в проводнике (Windows) или открыть его (Android).</summary>
    Task OpenFileInExplorerAsync(string filePath);

    /// <summary>Открыть папку с медиа поста.</summary>
    Task OpenFolderInExplorerAsync(string folderPath);
}
