using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.SubRooms.Contracts.Interfaces.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Запрос на присоединение к демонстрации экрана (как зритель)
/// </summary>
public sealed class ScreenDemoJoinRequestMessage : BaseMessage, IWithinSubRoom
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoJoinRequest;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <inheritdoc />
    public int SubRoomId { get; set; }
}
