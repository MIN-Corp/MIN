using MIN.Core.Messaging.Contracts;
using MIN.Sessions.Core.Messaging.Contracts.Enums;
using MIN.Sessions.Core.Messaging.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Enums;

namespace MIN.Sessions.Core.Messaging.OutOfSubRoom;

/// <summary>
/// Сообщение для сессии
/// </summary>
public sealed class SessionSpecificMessage : BaseSessionMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.SessionSpecific;

    /// <summary>
    /// Содержимое сообщения
    /// </summary>
    public required byte[] Body { get; set; }

    /// <summary>
    /// Роль экземпляра сессии отправителя сообщения
    /// </summary>
    public SessionInstanceRole SessionInstanceRole { get; set; }

    /// <summary>
    /// Направление сообщения сессии
    /// </summary>
    public SessionMessageRoute Route { get; set; }
}
