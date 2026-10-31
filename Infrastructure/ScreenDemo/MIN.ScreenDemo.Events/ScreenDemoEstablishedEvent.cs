using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.ScreenDemo.Events;

/// <summary>
/// Получено подтверждение доступа к демонстрации экрана.
/// Для стартовавшего — сигнал начать захват и предпросмотр,
/// для зрителя — сигнал открыть окно просмотра
/// </summary>
public sealed record ScreenDemoEstablishedEvent : BaseEvent, IRoomScopedEvent
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
