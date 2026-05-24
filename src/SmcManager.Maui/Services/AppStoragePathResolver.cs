using SmcManager.Core.Enums;

namespace SmcManager.Maui.Services;

/// <summary>
/// Вычисление корневых каталогов данных приложения.
/// </summary>
public static class AppStoragePathResolver
{
    public const string WindowsLocalParentFolder = "SHILY PROJECT";
    public const string WindowsLocalAppFolder = "SmcManager";
    public const string AndroidPublicFolderName = "SmcManager";
    public const string PortableDataFolderName = "Data";

    public static string ResolveDataRoot(AppStorageLocation location)
    {
        return location switch
        {
            AppStorageLocation.NextToExecutable => ResolveNextToExecutableRoot(),
            _ => ResolveDefaultLocalRoot()
        };
    }

    public static string GetLocationDescription(AppStorageLocation location, string dataRoot) =>
        location switch
        {
            AppStorageLocation.NextToExecutable =>
                $"Рядом с приложением: {dataRoot}",
            _ =>
#if WINDOWS
                $"Локальные данные: {dataRoot}",
#elif ANDROID
                $"Данные приложения: {dataRoot}",
#else
                $"Данные приложения: {dataRoot}",
#endif
        };

    public static string ResolveDefaultLocalRoot()
    {
#if WINDOWS
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            WindowsLocalParentFolder,
            WindowsLocalAppFolder);
#else
        return FileSystem.AppDataDirectory;
#endif
    }

    private static string ResolveNextToExecutableRoot()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(baseDir, PortableDataFolderName);
    }
}
