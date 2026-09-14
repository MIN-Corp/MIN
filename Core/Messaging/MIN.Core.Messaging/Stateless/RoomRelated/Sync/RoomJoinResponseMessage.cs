using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.Sync;

/// <summary>
/// Ответ на запрос о присоединения к комнате
/// </summary>
public sealed class RoomSyncResponseMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.RoomSyncResponse;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Упущенные сообщения
    /// </summary>
    public List<IMessage> MissedMessages { get; set; } = [];
}
