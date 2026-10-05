using MIN.Core.Stores.Contracts.Models.Persistence;

namespace MIN.Core.Stores.Contracts.Interfaces.Persistence;

/// <summary>
/// Преобразует состояние комнаты и её контекста в снимок и обратно
/// </summary>
public interface IRoomSnapshotMapper
{
    /// <summary>
    /// Собрать снимок комнаты из хранилищ
    /// </summary>
    /// <returns>
    /// null, если комнаты нет в RoomStore
    /// </returns>
    RoomSnapshot? BuildSnapshot(Guid roomId);

    /// <summary>
    /// Восстановить комнату из снимка
    /// </summary>
    void RestoreSnapshot(RoomSnapshot snapshot);
}
