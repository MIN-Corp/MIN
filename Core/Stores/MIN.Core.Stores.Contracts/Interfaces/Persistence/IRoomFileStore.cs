using MIN.Core.Stores.Contracts.Models.Persistence;

namespace MIN.Core.Stores.Contracts.Interfaces.Persistence;

/// <summary>
/// Хранилище файлов комнат (.mr)
/// </summary>
public interface IRoomFileStore
{
    /// <summary>
    /// Сохранить снимок комнаты в файл
    /// </summary>
    Task SaveAsync(Guid roomId, RoomSnapshot snapshot);

    /// <summary>
    /// Загрузить все сохранённые комнаты
    /// </summary>
    IReadOnlyList<RoomSnapshot> LoadAll();

    /// <summary>
    /// Удалить файл комнаты (.mr и .bak)
    /// </summary>
    void Delete(Guid roomId);

    /// <summary>
    /// Существует ли файл комнаты
    /// </summary>
    bool Exists(Guid roomId);
}
