using SmcManager.Core.Enums;
using SmcManager.Core.Interfaces;

namespace SmcManager.Maui.Services;

/// <summary>
/// Реализация путей хранения с созданием каталогов при старте.
/// </summary>
public sealed class AppStoragePaths : IAppStoragePaths
{
    public AppStoragePaths(AppStorageLocation location)
    {
        Location = location;
        DataRoot = EnsureDataRoot(AppStoragePathResolver.ResolveDataRoot(location));
        DatabasePath = Path.Combine(DataRoot, "smcmanager.db");
        DownloadsPath = Path.Combine(DataRoot, "downloads");
        LocationDescription = AppStoragePathResolver.GetLocationDescription(location, DataRoot);
    }

    public AppStorageLocation Location { get; }

    public string DataRoot { get; }

    public string DatabasePath { get; }

    public string DownloadsPath { get; }

    public string LocationDescription { get; }

    private static string EnsureDataRoot(string preferredRoot)
    {
        Directory.CreateDirectory(preferredRoot);
        Directory.CreateDirectory(Path.Combine(preferredRoot, "downloads"));
        return preferredRoot;
    }
}
