using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.Leaving;

/// <summary>
/// Потверждение хостом выхода участника
/// </summary>
public sealed class RoomLeaveMessageAckMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.RoomLeaveAck;

    /// <inheritdoc />
    public override bool IsPublic => false;
}
