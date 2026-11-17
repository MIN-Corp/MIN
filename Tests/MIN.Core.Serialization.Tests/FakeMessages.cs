using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Serialization.Tests;

/// <summary>
/// Сообщение-дубликат тега ChatTextMessage для проверки коллизий
/// </summary>
public sealed class DuplicateTagMessage : IMessage
{
    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <inheritdoc />
    public MessageTypeTag TypeTag => MessageTypeTag.ChatTextMessage;

    /// <inheritdoc />
    public Guid SenderId { get; set; }

    /// <inheritdoc />
    public Guid? RecipientId { get; init; }

    /// <inheritdoc />
    public bool IsPublic => RecipientId == null;

    /// <inheritdoc />
    public bool RequiresLocalDuplication => false;

    /// <inheritdoc />
    public bool RequiresEncryption => true;

    /// <inheritdoc />
    public bool RequireStreamAcks => false;

    /// <inheritdoc />
    public MessageChannel Channel => MessageChannel.Secure;
}
