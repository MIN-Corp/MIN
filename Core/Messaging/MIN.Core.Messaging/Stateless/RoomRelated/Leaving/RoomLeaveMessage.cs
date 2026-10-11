using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.Leaving;

/// <summary>
/// Сообщение о скором отключении связи, с указанием причины
/// </summary>
public sealed class RoomLeaveMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.RoomLeave;

    /// <inheritdoc />
    public override bool IsPublic => false;
}

