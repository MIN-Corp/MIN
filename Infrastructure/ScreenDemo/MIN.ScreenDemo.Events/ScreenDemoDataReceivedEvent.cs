using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.ScreenDemo.Events;

/// <summary>
/// Получен фрагмент кадра демонстрации экрана
/// </summary>
public sealed record ScreenDemoDataReceivedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// От кого получен фрагмент
    /// </summary>
    public Guid SharerId { get; init; }
}
