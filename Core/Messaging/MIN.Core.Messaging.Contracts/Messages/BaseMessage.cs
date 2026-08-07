using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Messaging.Contracts.Messages;

/// <summary>
/// Базовый класс для всех сообщений, передаваемых по сети
/// Предоставляет общие свойства Id и Timestamp
/// </summary>
public abstract class BaseMessage : IMessage
{
    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; } = DateTime.Now;

    /// <inheritdoc />
    public abstract MessageTypeTag TypeTag { get; }

    /// <inheritdoc />
    public Guid SenderId { get; set; }

    /// <inheritdoc />
    public virtual Guid? RecipientId { get; init; }

    /// <inheritdoc />
    public virtual bool RequiresLocalDuplication { get; }

    /// <inheritdoc />
    public virtual bool IsPublic => RecipientId == null;

    /// <inheritdoc />
    public virtual bool RequiresEncryption { get; } = true;

    /// <inheritdoc />
    public virtual bool RequireStreamAcks { get; }

    /// <inheritdoc />
    public virtual MessageChannel Channel { get; } = MessageChannel.Secure;
}
