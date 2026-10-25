using MIN.Core.Entities;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.Sync;

/// <summary>
/// Запрос на получения упущенной после отключения инфы о комнате
/// </summary>
public sealed class RoomSyncRequestMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.RoomSyncRequest;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Локальный участник (в случае смены имени оффлайн)
    /// </summary>
    public required Participant LocalParticipant { get; set; }

    /// <summary>
    /// Последнее сообщение
    /// </summary>
    public DateTime? MessagesAfterTimestamp { get; set; }

    /// <summary>
    /// Идентификатор последнего сообщения для tie break
    /// </summary>
    public Guid? MessagesAfterMessageId { get; set; }
}
