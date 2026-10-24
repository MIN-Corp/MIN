namespace MIN.Sessions.Core.Messaging.Contracts.Enums;

/// <summary>
/// Фрейм (тип сообщения) для туннеля
/// </summary>
public enum TunnelFrameType
{
    /// <summary>
    /// Данные сессии
    /// </summary>
    Data = 1,

    /// <summary>
    /// Игровой клиент участника подключился к локальному туннелю
    /// </summary>
    Hello = 2,

    /// <summary>
    /// Хост подключился к порту игры
    /// </summary>
    HelloOk = 3,

    /// <summary>
    /// Хост не смог подключиться к порту игры
    /// </summary>
    HelloFail = 4,

    /// <summary>
    /// Закрытие игрового соединения
    /// </summary>
    Close = 5,
}
