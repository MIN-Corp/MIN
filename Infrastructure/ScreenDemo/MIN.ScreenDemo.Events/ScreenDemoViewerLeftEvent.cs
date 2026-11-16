using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.ScreenDemo.Events;

/// <summary>
/// Получена информация об уходе зрителя из демонстрации экрана
/// </summary>
public sealed record ScreenDemoViewerLeftEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; init; }

    /// <summary>
    /// Ушедший зритель
    /// </summary>
    public Guid ViewerId { get; init; }
}
