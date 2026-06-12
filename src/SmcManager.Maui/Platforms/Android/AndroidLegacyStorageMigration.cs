using SmcManager.Maui.Services;

namespace SmcManager.Maui.Platforms.Android;

/// <summary>
/// Однократный импорт данных из Pictures/SmcManager в AppData (после неудачной попытки публичного хранения).
/// </summary>
public static class AndroidLegacyStorageMigration
{
    public static void TryImportFromPublicPictures()
    {
        try
        {
            var publicRoot = TryGetPublicPicturesRoot();
            if (publicRoot is null)
                return;

            var appData = FileSystem.AppDataDirectory;
            if (string.Equals(publicRoot, appData, StringComparison.OrdinalIgnoreCase))
                return;

            CopyIfMissing(Path.Combine(publicRoot, "smcmanager.db"), Path.Combine(appData, "smcmanager.db"));
            CopyDirectoryIfMissing(Path.Combine(publicRoot, "downloads"), Path.Combine(appData, "downloads"));
            ImportSettings(publicRoot, appData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidLegacyStorageMigration: {ex}");
        }
    }

    private static void ImportSettings(string publicRoot, string appData)
    {
        var settingsDir = Path.Combine(publicRoot, "settings");
        if (!Directory.Exists(settingsDir))
            return;

        foreach (var file in Directory.EnumerateFiles(settingsDir, "*.json"))
        {
            var name = Path.GetFileName(file);
            if (name is null)
                continue;

            var key = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrEmpty(key))
                continue;

            var json = File.ReadAllText(file).Trim();
            if (string.IsNullOrWhiteSpace(json))
                continue;

            if (key == "color_theme" && int.TryParse(json, out var theme))
                Preferences.Default.Set(key, theme);
            else
                Preferences.Default.Set(key, json);
        }
    }

    private static string? TryGetPublicPicturesRoot()
    {
        try
        {
            var pictures = global::Android.OS.Environment.GetExternalStoragePublicDirectory(
                global::Android.OS.Environment.DirectoryPictures);
            if (pictures?.AbsolutePath is not { } picturesPath)
                return null;

            var root = Path.Combine(picturesPath, AppStoragePathResolver.AndroidPublicFolderName);
            return Directory.Exists(root) ? root : null;
        }
        catch
        {
            return null;
        }
    }

    private static void CopyIfMissing(string source, string destination)
    {
        if (!File.Exists(source) || File.Exists(destination))
            return;

        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.Copy(source, destination);
    }

    private static void CopyDirectoryIfMissing(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
            return;

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(destinationDir, relative);
            CopyIfMissing(file, target);
        }
    }
}
