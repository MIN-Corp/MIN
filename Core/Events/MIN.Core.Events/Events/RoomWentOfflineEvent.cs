using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.Core.Events.Events;

/// <summary>
/// Событие, возникающее при отключении от комнаты
/// </summary>
public sealed record RoomWentOfflineEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Причина, по которой комната ушла в offline
    /// </summary>
    public string? Reason { get; init; }
}
