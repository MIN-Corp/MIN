using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;
using MIN.Core.Transport.Contracts.Enum;

namespace MIN.Core.Events.Events;

/// <summary>
/// Событие, возникающее при забытии комнаты
/// </summary>
public sealed record RoomDestroyedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Причина выхода
    /// </summary>
    public DisconnectReason Reason { get; init; } = DisconnectReason.LeftRoom;
}
