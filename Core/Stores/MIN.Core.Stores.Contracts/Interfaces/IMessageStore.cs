using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Constants;

namespace MIN.Core.Stores.Contracts.Interfaces;

/// <summary>
/// Хранилище сообщений для комнаты
/// </summary>
public interface IMessageStore
{
    /// <summary>
    /// Добавить сообщение
    /// </summary>
    void AddMessage(IMessage message, bool appendOnStart = false);

    /// <summary>
    /// Обновить сообщение
    /// </summary>
    void UpdateMessage(Guid id, IMessage message);

    /// <summary>
    /// Получить сообщение по Id
    /// </summary>
    IMessage? GetMessageById(Guid id);

    /// <summary>
    /// Получить количество сохранённых сообщений
    /// </summary>
    int GetMessageCount();

    /// <summary>
    /// Получить историю последних сообщений
    /// </summary>
    IEnumerable<IMessage> GetRecentHistory(int pageSize = StoreConstants.MessagesPageSize);

    /// <summary>
    /// Получить историю сообщений
    /// </summary>
    IEnumerable<IMessage> GetHistory(int? page = null, int? pageSize = null);

    /// <summary>
    /// Получить все сообщения, старше по времени
    /// </summary>
    IEnumerable<IMessage> GetMessagesOlderThan(DateTime? oldestLoadedTimestamp, Guid? oldestLoadedMessageId, int pageSize = StoreConstants.MessagesPageSize);

    /// <summary>
    /// Получить все сообщения, младше по времени
    /// </summary>
    IEnumerable<IMessage> GetMessagesNewerThan(DateTime? latestLoadedTimestamp, Guid? latestLoadedMessageId, int pageSize = StoreConstants.MessagesPageSize);

    /// <summary>
    /// Получить последнее сообщение (внизу)
    /// </summary>
    IMessage? GetLastMessage();

    /// <summary>
    /// Получить первое сообщение (вверху)
    /// </summary>
    IMessage? GetFirstMessage();

    /// <summary>
    /// Удалить сообщение по Id
    /// </summary>
    void RemoveMessage(Guid id);

    /// <summary>
    /// Очистить сообщения из комнаты
    /// </summary>
    void ClearMessages();
}
