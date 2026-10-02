using MIN.Core.Stores.Contracts.Models.SubRooms;

namespace MIN.Core.Stores.Contracts.Models.Persistence;

/// <summary>
/// Снимок подкомнат для сохранения в файл
/// </summary>
public sealed class SubRoomsSnapshot
{
    /// <summary>
    /// Список всех подкомнат
    /// </summary>
    public List<SubRoomInfo> SubRooms { get; set; } = null!;

    /// <summary>
    /// Следующий идентификатор для подкомнаты
    /// </summary>
    public int NextId { get; set; }
}
