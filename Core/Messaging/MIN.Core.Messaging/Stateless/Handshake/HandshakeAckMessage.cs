using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.Handshake;

/// <summary>
/// Подтверждение рукопожатия, содержащее публичный ключ сервера
/// </summary>
public sealed class HandshakeAckMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.HandshakeAck;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Подтверждение рукопожатия не шифруется
    /// </summary>
    public override bool RequiresEncryption => false;

    /// <summary>
    /// Хост комнаты
    /// </summary>
    public ParticipantInfo Participant { get; set; } = null!;

    /// <summary>
    /// Хеш публичного ключа хоста (в случае его несовпадения)
    /// </summary>
    /// <remarks>
    /// null - если уверены, что это первая встреча
    /// </remarks>
    public byte[]? PublicKeyFingerprint { get; set; } = null!;

    /// <summary>
    /// Публичный ключ хоста
    /// </summary>
    /// <remarks>
    /// null - если уже была встреча и нужно проверить сохранившееся
    /// </remarks>
    public byte[]? PublicKey { get; set; } = null!;
}
