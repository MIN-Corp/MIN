using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.Sessions.Core.Events;

/// <summary>
/// Туннель начал слушать локальный порт для игрового клиента
/// </summary>
public sealed record TunnelListenerStartedEvent : BaseEvent, IRoomScopedEvent
{
    /// <summary>
    /// Идентификатор комнаты
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; init; }

    /// <summary>
    /// Локальный порт туннеля
    /// </summary>
    public int Port { get; init; }
}
