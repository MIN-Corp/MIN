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
    public required string Body { get; set; }

    /// <summary>
    /// Роль приложения отправителя сообщения
    /// </summary>
    public SessionInstanceRole SessionProcessRole { get; set; }

    /// <summary>
    /// Направление сообщения сессии
    /// </summary>
    public SessionMessageRoute Route { get; set; }
}
