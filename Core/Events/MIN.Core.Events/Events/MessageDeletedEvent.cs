using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Events.Events;

/// <summary>
/// Сообщение было удалено
/// </summary>
public sealed record MessageDeletedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор удалённого сообщения
    /// </summary>
    public Guid MessageId { get; init; }

    /// <summary>
    /// Удалённое сообщение (копия)
    /// </summary>
    public IMessage? Message { get; init; }
}
