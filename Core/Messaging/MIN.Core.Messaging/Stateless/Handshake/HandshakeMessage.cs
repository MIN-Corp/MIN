using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.Handshake;

/// <summary>
/// Сообщения для обмена криптографической информации
/// </summary>
public sealed class HandshakeMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.Handshake;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Рукопожатие не шифруется (ключ ещё не установлен)
    /// </summary>
    public override bool RequiresEncryption => false;

    /// <summary>
    /// Информация об инициирующем участнике
    /// </summary>
    public ParticipantInfo Participant { get; init; } = null!;

    /// <summary>
    /// Версия приложения отправителя
    /// </summary>
    public Version Version { get; init; } = null!;

    /// <summary>
    /// Хеш публичного ключ (в случае его несовпадения)
    /// </summary>
    /// <remarks>
    /// Null - если уверены, что это первая встреча
    /// </remarks>
    public byte[]? PublicKeyFingerprint { get; init; }

    /// <summary>
    /// Публичный ключ
    /// </summary>
    /// <remarks>
    /// Null - если уже была встреча и нужно проверить сохранившееся
    /// </remarks>
    public byte[]? PublicKey { get; init; }
}
