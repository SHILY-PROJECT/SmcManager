namespace SmcManager.Maui.Services;

/// <summary>
/// История переходов между разделами flyout (Скачать, Контент, …) для кнопки/жеста «назад».
/// </summary>
public static class ShellNavigationHistory
{
    private static readonly Stack<string> Routes = new();
    private static bool _suppressNextRecord;

    public static void ResetToRoute(string route)
    {
        Routes.Clear();
        Routes.Push(route);
    }

    /// <summary>
    /// Запоминает выбор раздела в боковом меню.
    /// </summary>
    public static void RecordFlyoutNavigation(string route)
    {
        if (_suppressNextRecord)
        {
            _suppressNextRecord = false;
            return;
        }

        if (Routes.Count > 0 && Routes.Peek() == route)
            return;

        Routes.Push(route);
    }

    /// <summary>
    /// Возвращает маршрут предыдущего раздела или null, если выходить из приложения.
    /// </summary>
    public static bool TryPopToPreviousRoute(out string previousRoute)
    {
        if (Routes.Count <= 1)
        {
            previousRoute = string.Empty;
            return false;
        }

        Routes.Pop();
        previousRoute = Routes.Peek();
        return true;
    }

    public static void PrepareBackNavigation() => _suppressNextRecord = true;
}
