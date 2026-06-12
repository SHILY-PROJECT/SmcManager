namespace SmcManager.Core.Enums;

/// <summary>
/// Где хранятся база данных и скачанные файлы.
/// </summary>
public enum AppStorageLocation
{
    /// <summary>Стандартная папка данных приложения (на Windows — %LocalAppData%\SHILY PROJECT\SmcManager).</summary>
    DefaultLocal = 0,

    /// <summary>Папка Data рядом с исполняемым файлом (портативный режим).</summary>
    NextToExecutable = 1
}
