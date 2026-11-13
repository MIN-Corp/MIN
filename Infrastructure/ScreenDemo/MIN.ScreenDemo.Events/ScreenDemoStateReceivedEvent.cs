using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Events;

/// <summary>
/// Получена информация об активных демонстрациях экрана в комнате
/// </summary>
public sealed record ScreenDemoStateReceivedEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Активные демонстрации в комнате
    /// </summary>
    public List<ScreenDemoStateEntry> Demos { get; init; } = [];
}
