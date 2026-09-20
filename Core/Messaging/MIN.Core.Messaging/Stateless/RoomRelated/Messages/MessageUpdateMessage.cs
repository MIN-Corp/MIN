using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.Messages;

/// <summary>
/// Действие редактирования сообщение из чата
/// </summary>
public sealed class MessageUpdateMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.MessageUpdate;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <summary>
    /// Идентификатор редактируемого сообщения
    /// </summary>
    public required Guid MessageIdToEdit { get; set; }

    /// <summary>
    /// Новый контент для сообщения
    /// </summary>
    public required IMessage NewMessage { get; set; }
}
