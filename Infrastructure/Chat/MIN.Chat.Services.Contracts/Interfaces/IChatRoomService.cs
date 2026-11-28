using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;

namespace MIN.Chat.Services.Contracts.Interfaces;

/// <summary>
/// Сервис для работы с возможностями комнаты (кик, запрос на историю сообщений и т.д.)
/// </summary>
public interface IChatRoomService
{
    /// <summary>
    /// Кикнуть участника с причиной
    /// </summary>
    Task KickParticipantAsync(Guid roomId, Guid participantId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправить обновлённые данные о комнате
    /// </summary>
    Task SendUpdatedRoomInfoAsync(RoomInfo updatedRoomInfo, IEnumerable<IEndpoint> endpoints, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправить запрос на обновление чата
    /// </summary>
    Task SendChatHistoryRequest(Guid roomId, DateTime? oldestTimestamp, Guid? oldestMessageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Настроить discovery по настройкам
    /// </summary>
    Task ManageDiscoveryOutOfSettings(RoomInfo room, IEnumerable<IEndpoint> endpoints, NetworkOptions newNetworkOptions, NetworkOptions? oldNetworkOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновить настройки сети по комнате
    /// </summary>
    Task UpdateNetworkOutOfSettings(RoomInfo room, IEnumerable<IEndpoint> endpoints, NetworkOptions newNetworkOptions, NetworkOptions? oldNetworkOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Забанить участника на выбранную длительность, опционально по IP, и исключить его из комнаты
    /// </summary>
    Task BanParticipantAsync(Guid roomId, Guid participantId, string participantName, string reason, BanDuration duration, bool alsoBanIp = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Разбанить участника комнаты
    /// </summary>
    Task UnbanParticipantAsync(Guid roomId, Guid participantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменить причину или дату разбана участника
    /// </summary>
    Task<BanInfo?> UpdateBanAsync(Guid roomId, Guid participantId, string reason, BanDuration duration, bool alsoBanIp = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Известен ли ip адрес участника как хоста
    /// </summary>
    bool ParticipantIpAddressIsKnown(Guid roomId, Guid participantId);

    /// <summary>
    /// Получить список забаненных участников комнаты
    /// </summary>
    Task<IReadOnlyList<BanInfo>> GetBannedParticipantsAsync(Guid roomId, CancellationToken cancellationToken = default);
}
