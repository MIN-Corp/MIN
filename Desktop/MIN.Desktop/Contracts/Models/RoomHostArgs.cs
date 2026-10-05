using System;
using System.Threading.Tasks;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Transport.Contracts.Models;

namespace MIN.Desktop.Contracts.Models;

/// <summary>
/// Аргументы для хостинга комнаты
/// </summary>
public class RoomHostArgs
{
    /// <summary>
    /// Информация о комнате
    /// </summary>
    public required RoomInfo RoomInfo { get; init; }

    /// <summary>
    /// Настройки сети
    /// </summary>
    public NetworkOptions NetworkOptions { get; init; }

    /// <summary>
    /// Событие по готовности комнаты
    /// </summary>
    public required Func<Room, Task> OnRoomReady { get; init; }
}
