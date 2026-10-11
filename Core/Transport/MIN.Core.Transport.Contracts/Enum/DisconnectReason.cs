using System.ComponentModel;

namespace MIN.Core.Transport.Contracts.Enum;

/// <summary>
/// Причина, по которой оборвалась связь
/// </summary>
public enum DisconnectReason
{
    /// <summary>
    /// Без причины
    /// </summary>
    /// <remarks>
    /// Для участника: по умолчанию считается, что хост остановил комнату
    /// Для хоста: по умолчанию считается, что участник вышел и приложения
    /// </remarks>
    [Description("Хост остановил комнату")]
    None,

    /// <summary>
    /// Умышленно отключён
    /// </summary>
    [Description("Отключились от комнаты")]
    Intentionally,

    /// <summary>
    /// Вышло время подключения
    /// </summary>
    [Description("Вышло время ожидания подключения")]
    Timeout,

    /// <summary>
    /// Произошла ошибка
    /// </summary>
    [Description("Произошла ошибка")]
    Error,

    /// <summary>
    /// Произошла ошибка прохождения протокола
    /// </summary>
    [Description("Произошла ошибка прохождения протокола MIN")]
    ProtocolError,

    /// <summary>
    /// Хост намеренно кикнул участника
    /// </summary>
    [Description("Хост кикнул тебя")]
    Kick,

    /// <summary>
    /// Участник намеренно вышел из комнаты (забыл её)
    /// </summary>
    [Description("Комната забыта")]
    LeftRoom,
}
