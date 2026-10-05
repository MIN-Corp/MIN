using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.Handshake;

/// <summary>
/// Сообщения для ответа на получения публичного ключа
/// </summary>
public sealed class PublicKeyResponseMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.PublicKeyResponse;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Публичного ключ не шифруется (ключ ещё не установлен)
    /// </summary>
    public override bool RequiresEncryption => false;

    /// <summary>
    /// Публичный ключ
    /// </summary>
    public byte[] PublicKey { get; set; } = null!;
}
