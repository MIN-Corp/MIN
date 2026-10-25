using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.Core.Events.Events;

/// <summary>
/// Событие, возникающее при получении Ping-Pong сообщения
/// </summary>
public sealed record PingPongReceivedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Роль получения сообщения
    /// </summary>
    public Role Role { get; init; }

    /// <summary>
    /// Идентификатор соединения
    /// </summary>
    public Guid ConnectionId { get; init; }
}
