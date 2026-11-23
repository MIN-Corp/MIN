using MIN.Core.Entities.Contracts.Models;

namespace MIN.Core.Services.Contracts.Interfaces.Moderation;

/// <summary>
/// Сервис управления банами участников комнаты
/// </summary>
public interface IBanService
{
    /// <summary>
    /// Забанить участника комнаты
    /// </summary>
    /// <returns>Информация о созданном бане</returns>
    BanInfo Ban(Guid roomId, Guid participantId, string name, string reason, DateTimeOffset? bannedUntil);

    /// <summary>
    /// Разбанить участника комнаты
    /// </summary>
    /// <returns>True - если участник был в списке банов</returns>
    bool Unban(Guid roomId, Guid participantId);

    /// <summary>
    /// Изменить дату разбана участника
    /// </summary>
    /// <returns>True - если бан найден и обновлён</returns>
    bool UpdateBan(Guid roomId, Guid participantId, DateTimeOffset? bannedUntil);

    /// <summary>
    /// Проверить, действует ли бан на участника
    /// </summary>
    /// <remarks>
    /// Истёкшие баны удаляются лениво при проверке
    /// </remarks>
    bool TryGetActiveBan(Guid roomId, Guid participantId, out BanInfo ban);

    /// <summary>
    /// Получить список забаненных участников комнаты
    /// </summary>
    /// <remarks>
    /// Истёкшие баны удаляются при получении списка
    /// </remarks>
    IReadOnlyList<BanInfo> GetBannedParticipants(Guid roomId);
}
