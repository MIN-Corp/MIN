using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.ScreenDemo.Events;

/// <summary>
/// Получена информация о завершении демонстрации экрана
/// </summary>
public sealed record ScreenDemoEndedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; init; }

    /// <summary>
    /// Кто демонстрировал экран
    /// </summary>
    public Guid SharerId { get; init; }
}
