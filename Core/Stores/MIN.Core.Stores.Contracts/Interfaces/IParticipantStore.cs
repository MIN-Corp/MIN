using MIN.Core.Entities;

namespace MIN.Core.Stores.Contracts.Interfaces;

/// <summary>
/// Хранилище участников для комнаты
/// </summary>
public interface IParticipantStore
{
    /// <summary>
    /// Привязать хранилище к списку участников комнаты
    /// </summary>
    /// <remarks>
    /// Все последующие изменения будут производится напрямую в переданном списке,
    /// чтобы он всегда оставался актуальным
    /// </remarks>
    void Bind(List<Participant> participants);

    /// <summary>
    /// Добавить участника в комнату
    /// </summary>
    void AddParticipant(Participant participant);

    /// <summary>
    /// Обновить участника
    /// </summary>
    void UpdateParticipant(Guid id, Participant participant);

    /// <summary>
    /// Удалить участника из комнаты
    /// </summary>
    void RemoveParticipant(Guid participantId);

    /// <summary>
    /// Получить участника комнаты
    /// </summary>
    Participant GetParticipantById(Guid participantId);

    /// <summary>
    /// Получить участников комнаты по их идентификаторам
    /// </summary>
    IEnumerable<Participant> GetParticipantByIds(IEnumerable<Guid> participantId);

    /// <summary>
    /// Попытаться получить участника комнаты
    /// </summary>
    bool TryGetParticipantById(Guid participantId, out Participant? participant);

    /// <summary>
    /// Получить список всех участников комнаты
    /// </summary>
    IEnumerable<Participant> GetParticipants();

    /// <summary>
    /// Пометить всех участников как оффлайн для комнаты
    /// </summary>
    void MarkAllParticipansOffline(Guid? exceptId = null);

    /// <summary>
    /// Очистить участников для комнаты
    /// </summary>
    void ClearParticipants();
}
