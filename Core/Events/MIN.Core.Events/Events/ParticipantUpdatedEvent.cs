using MIN.Core.Entities;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.Core.Events.Events;

/// <summary>
/// Участник обновил данные о себе
/// </summary>
public sealed record ParticipantUpdatedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Новые данные об участнике
    /// </summary>
    public required Participant NewParticipant { get; init; }
}
