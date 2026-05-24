using SmcManager.Maui.Services;

namespace SmcManager.Maui.Platforms.Android;

/// <summary>
/// Дублирует скачанные медиа в Pictures/SmcManager (остаются в галерее после удаления приложения).
/// </summary>
public static class AndroidPublicMediaExporter
{
    public static void TryMirror(string localFilePath)
    {
        if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
            return;

        try
        {
            var publicRoot = TryGetPublicPicturesRoot();
            if (publicRoot is null)
                return;

            var appData = FileSystem.AppDataDirectory;
            var downloadsRoot = Path.Combine(appData, "downloads");
            if (!localFilePath.StartsWith(downloadsRoot, StringComparison.OrdinalIgnoreCase))
                return;

            var relative = Path.GetRelativePath(downloadsRoot, localFilePath);
            var target = Path.Combine(publicRoot, "downloads", relative);
            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            File.Copy(localFilePath, target, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidPublicMediaExporter: {ex}");
        }
    }

    private static string? TryGetPublicPicturesRoot()
    {
        try
        {
            if (global::Android.OS.Environment.ExternalStorageState != global::Android.OS.Environment.MediaMounted)
                return null;

            var pictures = global::Android.OS.Environment.GetExternalStoragePublicDirectory(
                global::Android.OS.Environment.DirectoryPictures);
            if (pictures?.AbsolutePath is not { } picturesPath)
                return null;

            var root = Path.Combine(picturesPath, AppStoragePathResolver.AndroidPublicFolderName);
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "downloads"));
            return root;
        }
        catch
        {
            return null;
        }
    }
}
