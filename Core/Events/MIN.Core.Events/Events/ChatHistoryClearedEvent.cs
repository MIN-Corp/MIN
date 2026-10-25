using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;
using MIN.Core.Messaging.Stateless.RoomRelated.History;

namespace MIN.Core.Events.Events;

/// <summary>
/// Событие, возникающее при очищении истории чата
/// </summary>
public sealed record ChatHistoryClearedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Сообщение удаления истории чата
    /// </summary>
    public required ChatHistoryClearMessage Message { get; init; }
}
