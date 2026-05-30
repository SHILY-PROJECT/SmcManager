namespace SmcManager.Maui.Services;

/// <summary>
/// Ожидание готовности локальных медиафайлов сразу после скачивания.
/// </summary>
internal static class MediaFileReadiness
{
    public static async Task WaitForFilesAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default,
        int maxTotalMilliseconds = 350)
    {
        var deadline = Environment.TickCount64 + maxTotalMilliseconds;

        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            while (Environment.TickCount64 < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsReady(path))
                    break;

                await Task.Delay(30, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsReady(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            var info = new FileInfo(path);
            if (info.Length <= 0)
                return false;

            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
