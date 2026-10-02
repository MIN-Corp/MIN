using MIN.Core.Entities;

namespace MIN.Core.Stores.Contracts.Models.Persistence;

/// <summary>
/// Конверт файла комнаты
/// </summary>
/// <remarks>
/// Сообщения хранятся отдельными json-строками, чтобы повреждение одного
/// сообщения не убивало всю комнату
/// </remarks>
public sealed class RoomFileEnvelope
{
    /// <summary>
    /// Версия схемы
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Комната
    /// </summary>
    public Room? Room { get; set; }

    /// <summary>
    /// Json сообщений
    /// </summary>
    public List<string> Messages { get; set; } = [];

    /// <summary>
    /// Состояние подкомнат
    /// </summary>
    public SubRoomsSnapshot? SubRooms { get; set; }
}
