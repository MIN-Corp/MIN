using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;
using MIN.Core.Messaging.RoomRelated.ParticipantRelated;

namespace MIN.Core.Events.Events;

/// <summary>
/// Событие, возникающее при покидании участником комнаты
/// (с ним нельзя будет связяться, но может считаться в списке если он вышел непреднамеренно)
/// </summary>
public sealed record ParticipantLeftEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Сообщение о вышедшем участнике
    /// </summary>
    public ParticipantLeftMessage Message { get; init; } = null!;
}
