namespace MIN.Core.Stores.Contracts.Models.SubRooms;

/// <summary>
/// Состояние подкомнат с счётчиком
/// </summary>
public sealed class SubRoomState
{
    /// <summary>
    /// Словарь подкомнат
    /// </summary>
    public readonly Dictionary<int, SubRoomInfo> SubRooms = [];

    /// <summary>
    /// Идентификатор следующей добавленной подкомнаты
    /// </summary>
    public int NextId = 1;
}
