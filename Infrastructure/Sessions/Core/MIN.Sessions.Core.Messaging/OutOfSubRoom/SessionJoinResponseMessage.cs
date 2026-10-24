using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Sessions.Core.Messaging.OutOfSubRoom;

/// <summary>
/// Сообщение ответа на присоединение к сессии
/// </summary>
public sealed class SessionJoinResponseMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.SessionJoinResponse;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Идентификатор сессии
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Флаг, указывающий, нужно ли оповещать остальных
    /// </summary>
    public bool NeedToAnnounce { get; init; }

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; init; }
}
