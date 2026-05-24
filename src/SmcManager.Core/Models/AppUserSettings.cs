using SmcManager.Core.Enums;

namespace SmcManager.Core.Models;

/// <summary>
/// Пользовательские настройки приложения.
/// </summary>
public class AppUserSettings
{
    /// <summary>Количество элементов в блоке «Последние скачивания» (10, 20 или 30).</summary>
    public int RecentDownloadsCount { get; set; } = 10;

    /// <summary>true — дата в папке из публикации; false — дата скачивания.</summary>
    public bool UsePostedDateForFolder { get; set; } = true;

    /// <summary>true — на вкладке «Скачать» по умолчанию «Без аккаунта» (без cookies).</summary>
    public bool PreferDownloadWithoutAccount { get; set; } = false;

    /// <summary>Последний выбор авторизованного аккаунта для скачивания (ключ — имя платформы, значение — id аккаунта).</summary>
    public Dictionary<string, int> LastDownloadAccountIdByPlatform { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Где хранить БД и скачанные файлы (применяется после перезапуска).</summary>
    public AppStorageLocation StorageLocation { get; set; } = AppStorageLocation.DefaultLocal;

    /// <summary>Последнее состояние expand/collapse области медиа на экране просмотра контента.</summary>
    public bool IsContentMediaExpanded { get; set; }

    public static IReadOnlyList<int> AllowedRecentCounts { get; } = [10, 20, 30];

    public void Normalize()
    {
        if (!AllowedRecentCounts.Contains(RecentDownloadsCount))
            RecentDownloadsCount = 10;
    }
}
