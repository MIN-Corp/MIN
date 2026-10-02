using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.Handshake;

/// <summary>
/// Сообщения для запроса на получение публичного ключа
/// </summary>
public sealed class PublicKeyRequestMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.PublicKeyRequest;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Публичного ключ не шифруется (ключ ещё не установлен)
    /// </summary>
    public override bool RequiresEncryption => false;
}
