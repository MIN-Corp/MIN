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
    /// Рукопожатие
    /// </summary>
    Hello,

    /// <summary>
    /// Подтверждение рукопожатия
    /// </summary>
    HelloOk,

    /// <summary>
    /// Рукопожатие провалилось
    /// </summary>
    HelloFail,

    /// <summary>
    /// Туннель закрылся
    /// </summary>
    Close
}
