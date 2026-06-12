using SmcManager.Core.Enums;

namespace SmcManager.Core.Interfaces;

/// <summary>
/// Корневые пути для базы данных и скачанного контента.
/// </summary>
public interface IAppStoragePaths
{
    AppStorageLocation Location { get; }

    /// <summary>Корень данных (БД и подкаталог downloads).</summary>
    string DataRoot { get; }

    string DatabasePath { get; }

    string DownloadsPath { get; }

    /// <summary>Человекочитаемое описание текущего расположения для UI.</summary>
    string LocationDescription { get; }
}
