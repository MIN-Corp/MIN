using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.ScreenDemo.Events;

/// <summary>
/// Получена информация о начале демонстрации экрана в комнате
/// </summary>
public sealed record ScreenDemoStartedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; init; }

    /// <summary>
    /// Кто демонстрирует экран
    /// </summary>
    public Guid SharerId { get; init; }
}
