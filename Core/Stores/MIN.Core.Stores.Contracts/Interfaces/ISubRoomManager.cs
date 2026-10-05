using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Core.Stores.Contracts.Models.Persistence;
using MIN.Core.Stores.Contracts.Models.SubRooms;

namespace MIN.Core.Stores.Contracts.Interfaces;

/// <summary>
/// Менеджер по подкомнатам
/// </summary>
public interface ISubRoomManager
{
    /// <summary>
    /// Захостить подкомнату
    /// </summary>
    SubRoomInfo HostSubRoom(ParticipantInfo creator, SubRoomPurpose purpose, int? maximum = null);

    /// <summary>
    /// Запустить подкомнату
    /// </summary>
    bool ActivateSubRoom(int subRoomId, ParticipantInfo participant);

    /// <summary>
    /// Попытаться войти в подкомнату
    /// </summary>
    SubRoomJoinOutcome TryJoinSubRoom(int subRoomId, ParticipantInfo participant);

    /// <summary>
    /// Находиться ли участник внутри подкомнаты
    /// </summary>
    bool IsInSubRoom(int subRoomId, Guid participantId);

    /// <summary>
    /// Уйти из подкомнаты и деактивирует в случае выхода последнего участника
    /// </summary>
    /// <returns>
    /// true - если комната ещё активна
    /// false - если нет
    /// </returns>
    bool LeaveSubRoom(int subRoomId, Guid participantId);

    /// <summary>
    /// Попытаться остановить подкомнаты
    /// </summary>
    /// <remarks>
    /// Доступно только хосту или инициатору подкомнаты
    /// </remarks>
    bool TryStopSubRoom(int subRoomId, Guid requesterId);

    /// <summary>
    /// Получить список участников подкомнаты
    /// </summary>
    IReadOnlyList<Guid> GetParticipantIds(int subRoomId);

    /// <summary>
    /// Получить количество участников подкомнаты
    /// </summary>
    int GetParticipantCount(int subRoomId);

    /// <summary>
    /// Получить информацию о подкомнате
    /// </summary>
    SubRoomInfo? GetSubRoom(int subRoomId);

    /// <summary>
    /// Получить все подкомнаты комнаты
    /// </summary>
    IReadOnlyList<SubRoomInfo> GetRoomSubRooms();

    /// <summary>
    /// Удалить подкомнату
    /// </summary>
    void RemoveSubRoom(int subRoomId);

    /// <summary>
    /// Очистить все подкомнаты для комнаты
    /// </summary>
    void ClearRoomSubRooms();

    /// <summary>
    /// Создать снимок состояния
    /// </summary>
    SubRoomsSnapshot CreateSnapshot();

    /// <summary>
    /// Востановить состояние со снимка
    /// </summary>
    void RestoreState(SubRoomsSnapshot subRoomsSnapshot);
}
