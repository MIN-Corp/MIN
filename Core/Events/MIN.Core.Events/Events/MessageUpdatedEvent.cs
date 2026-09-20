using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Events.Events;

/// <summary>
/// Сообщение было отредактировано
/// </summary>
public sealed record MessageUpdatedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор отредактированного сообщения
    /// </summary>
    public Guid MessageId { get; init; }

    /// <summary>
    /// Обновлённое сообщение
    /// </summary>
    public required IContentEditable Message { get; init; }
}
