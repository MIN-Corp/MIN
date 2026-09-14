using MIN.Core.Entities.Contracts.Models;

namespace MIN.Core.Stores.Contracts.Registries.Interfaces;

/// <summary>
/// Сервис по ассоциации участников и соеднинений
/// </summary>
public interface IParticipantConnectionRegistry
{
    /// <summary>
    /// Установить ассоциацию соеднинения с участником
    /// </summary>
    void Register(Guid connectionId, ParticipantInfo participant);

    /// <summary>
    /// Есть ли какой-то участник с таким идентификатором соединения
    /// </summary>
    bool ConnectionExists(Guid connectionId);

    /// <summary>
    /// Установить ассоциацию соеднинения с локальным участником
    /// </summary>
    void RegisterLocalParticipant(ParticipantInfo participant);

    /// <summary>
    /// Получить данные участника от его соединения
    /// </summary>
    ParticipantInfo? GetParticipant(Guid connectionId);

    /// <summary>
    /// Попытаться получить данные участника от его соединения
    /// </summary>
    bool TryGetParticipantFromConnectionId(Guid connectionId, out ParticipantInfo participant);

    /// <summary>
    /// Получить идентификтор участника от идентификатора соеднинения
    /// </summary>
    Guid GetParticipantIdFromConnectionId(Guid connectionId);

    /// <summary>
    /// Получить идентификтор соеднинения от идентификатора участника
    /// </summary>
    Guid GetConnectionIdFromParticipantId(Guid participantId);

    /// <summary>
    /// Попытаться получить идентификатор соединения от идентификатора участника
    /// </summary>
    bool TryGetConnectionIdFromParticipantId(Guid participantId, out Guid connectionId);

    /// <summary>
    /// Разорвать ассоциацию соеднинения с участником
    /// </summary>
    void Unregister(Guid connectionId);
}
