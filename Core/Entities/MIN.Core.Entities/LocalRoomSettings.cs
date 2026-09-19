using MIN.Core.Transport.Contracts.Models;

namespace MIN.Core.Entities;

/// <summary>
/// Локальные настройки комнаты
/// </summary>
public record LocalRoomSettings
{
    /// <summary>
    /// Включены ли уведомления
    /// </summary>
    public bool NotificationsEnabled { get; set; }

    /// <summary>
    /// Настройки глобальности сети
    /// </summary>
    public NetworkOptions NetworkOptions { get; set; }

    /// <summary>
    /// Время последнего сообщения, перед которым испория была очищена
    /// </summary>
    /// <remarks>
    /// null - если историю не очищали
    /// </remarks>
    public DateTime? HistoryWipedOutUpTo { get; set; }

    /// <summary>
    /// Участники, которых нужно кикнуть при следующей попытке входа
    /// </summary>
    public Dictionary<Guid, string> PendingKickParticipantIds { get; set; } = [];
}
