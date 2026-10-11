using MIN.Core.Entities;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Stores.Contracts.Models.Persistence;

/// <summary>
/// Снимок комнаты для сохранения её в файл
/// </summary>
public sealed class RoomSnapshot
{
    /// <summary>
    /// Комната и её участники
    /// </summary>
    public required Room Room { get; set; }

    /// <summary>
    /// Полная история сообщений из MessageStore
    /// </summary>
    public required IReadOnlyCollection<IMessage> Messages { get; set; }

    /// <inheritdoc cref="SubRoomsSnapshot"/>
    public SubRoomsSnapshot? SubRooms { get; set; }
}
