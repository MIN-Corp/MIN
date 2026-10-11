using MIN.Core.Entities;

namespace MIN.Core.Stores.Contracts.Models.Persistence;

/// <summary>
/// Контекст миграции сообщений (вся инфа, чтобы восстановить данные)
/// </summary>
public sealed class RoomMigrationContext
{
    /// <summary>
    /// Комната
    /// </summary>
    public required Room Room { get; init; }

    // сюда же позже можно добавить SubRooms, если понадобятся
}
