namespace SmcManager.Maui.Services;

/// <summary>
/// Ожидание первой навигации Shell (важно для Share intent при холодном старте на Android).
/// </summary>
internal static class ShellReadiness
{
    private static TaskCompletionSource _ready = CreateSource();
    private static int _marked;

    public static Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _marked) == 1)
            return Task.CompletedTask;

        return _ready.Task.WaitAsync(cancellationToken);
    }

    public static void MarkReady()
    {
        if (Interlocked.Exchange(ref _marked, 1) == 1)
            return;

        _ready.TrySetResult();
    }

    private static TaskCompletionSource CreateSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
