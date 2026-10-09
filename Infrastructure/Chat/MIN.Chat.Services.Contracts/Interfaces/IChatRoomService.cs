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
}
