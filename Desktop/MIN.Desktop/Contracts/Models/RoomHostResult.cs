using MIN.Core.Entities;
using MIN.Desktop.Contracts.Models.Enums;

namespace MIN.Desktop.Contracts.Models;

/// <summary>
/// Результат хостинга комнаты
/// </summary>
public class RoomHostResult
{
    /// <summary>
    /// Ошибка хостинга
    /// </summary>
    public HostFailure? Failure { get; init; }

    /// <summary>
    /// Готовая комната
    /// </summary>
    public Room? Room { get; init; }

    /// <summary>
    /// Текст ошибки
    /// </summary>
    public string? ErrorMessage { get; init; }
}
