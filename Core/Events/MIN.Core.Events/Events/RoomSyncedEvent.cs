using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Events.Events;

/// <summary>
/// Событие, возникающее при получении упущенной информации о комнате
/// </summary>
public sealed record RoomSyncedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Упущенные сообщения
    /// </summary>
    public List<IMessage> MissedMessages { get; init; } = null!;
}
