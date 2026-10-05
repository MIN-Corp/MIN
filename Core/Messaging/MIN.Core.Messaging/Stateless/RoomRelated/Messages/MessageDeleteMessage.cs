using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.Messages;

/// <summary>
/// Действие удаления сообщение из чата
/// </summary>
public sealed class MessageDeleteMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.MessageDelete;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <summary>
    /// Идентификатор удаляемого сообщения
    /// </summary>
    public Guid MessageIdToDelete { get; set; }
}
