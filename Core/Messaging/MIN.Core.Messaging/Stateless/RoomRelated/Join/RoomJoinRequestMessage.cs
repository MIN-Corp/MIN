using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.Join;

/// <summary>
/// Запрос на присоединения к комнате
/// </summary>
public sealed class RoomJoinRequestMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.RoomJoinRequest;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Верификатор пароля комнаты
    /// </summary>
    /// <remarks>
    /// Совпадает с сохранённым на хосте; null - если пароль не требуется или не введён
    /// </remarks>
    public string? PasswordHash { get; init; }
}
