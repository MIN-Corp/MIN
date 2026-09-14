using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.Disconnect;

/// <summary>
/// Сообщение о скором отключении связи, с указанием причины
/// </summary>
public sealed class DisconnectMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.Disconnect;

    /// <inheritdoc />
    public override bool RequiresEncryption => false;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Причина отключения
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

