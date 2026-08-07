using MIN.Core.Messaging.Contracts.Enums;

namespace MIN.Core.Messaging.Contracts.Interfaces;

/// <summary>
/// Базовый интерфейс для всех сообщений, передаваемых по сети
/// </summary>
public interface IMessage
{
    /// <summary>
    /// Идентификатор сообщения
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Временная метка создания сообщения
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Тег типа сообщения для маршрутизации сообщения
    /// </summary>
    MessageTypeTag TypeTag { get; }

    /// <summary>
    /// Идентификатор отправителя сообщения
    /// </summary>
    Guid SenderId { get; set; }

    /// <summary>
    /// Идентификатор получателя сообщения
    /// </summary>
    /// <remarks>
    /// null - если оно должно разослаться всем
    /// </remarks>
    Guid? RecipientId { get; init; }

    /// <summary>
    /// Флаг, указывающий, должно ли сообщение рассылаться всем
    /// </summary>
    bool IsPublic { get; }

    /// <summary>
    /// Флаг, указывающий, требуется ли локальное дублирования сообщения
    /// </summary>
    bool RequiresLocalDuplication { get; }

    /// <summary>
    /// Флаг, указывающий, требуется ли шифрование для этого сообщения
    /// </summary>
    bool RequiresEncryption { get; }

    /// <summary>
    /// Флаг, указывающий, требуется ли подтверждение получения пакета
    /// при передаче в потоке для этого сообщения
    /// </summary>
    bool RequireStreamAcks { get; }

    /// <summary>
    /// Канал, по которому пойдёт сообщение
    /// </summary>
    MessageChannel Channel { get; }
}
