namespace MIN.Desktop.Contracts.Constants;

/// <summary>
/// Константы для Desktop
/// </summary>
public static class DesktopConstants
{
    /// <summary>
    /// Максимальное количество участников в одной комнате
    /// </summary>
    public const int MaximumParticipantsInRoom = 20;

    /// <summary>
    /// Не распознаный компьютер
    /// </summary>
    public const string UndefinedPcName = "ХЗ";

    /// <summary>
    /// Таймаут на подключение к комнате (мс)
    /// </summary>
    public const int RoomConnectionTimeoutMs = 10_000;

    /// <summary>
    /// При выходе из MIN он будет сохраняться в трее
    /// </summary>
#if DEBUG
    public const bool MinimizeToTrayEnabled = false;
#else
    public const bool MinimizeToTrayEnabled = true;
#endif
}
