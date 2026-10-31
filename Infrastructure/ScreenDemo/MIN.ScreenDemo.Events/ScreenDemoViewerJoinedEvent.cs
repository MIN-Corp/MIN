using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.ScreenDemo.Events;

/// <summary>
/// Получена информация о присоединении зрителя к демонстрации экрана
/// </summary>
public sealed record ScreenDemoViewerJoinedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; init; }

    /// <summary>
    /// Присоединившийся зритель
    /// </summary>
    public Guid ViewerId { get; init; }
}
