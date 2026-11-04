using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.SubRooms.Contracts.Interfaces.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Сообщение выхода зрителя из демонстрации экрана (отправляется хосту)
/// </summary>
public sealed class ScreenDemoLeaveMessage : BaseMessage, IWithinSubRoom
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoLeave;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <inheritdoc />
    public int SubRoomId { get; set; }
}
