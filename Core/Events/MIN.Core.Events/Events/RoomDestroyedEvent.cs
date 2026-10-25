using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.Core.Events.Events;

/// <summary>
/// Событие, возникающее при забытее комнаты
/// </summary>
public sealed record RoomDestroyedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }
}
