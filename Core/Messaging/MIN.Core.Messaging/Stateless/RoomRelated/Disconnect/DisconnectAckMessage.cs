using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.Disconnect;

/// <summary>
/// Ответ на то, что получатель уведомлён о причине отсоединения
/// </summary>
public sealed class DisconnectAckMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.DisconnectAck;

    /// <inheritdoc />
    public override bool RequiresEncryption => false;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Причина отключения
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
