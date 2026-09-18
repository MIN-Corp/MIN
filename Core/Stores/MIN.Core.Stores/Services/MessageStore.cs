using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces;

namespace MIN.Core.Stores.Services;

/// <inheritdoc cref="IMessageStore"/>
public sealed class MessageStore : IMessageStore
{
    private readonly List<IMessage> messages = [];

    void IMessageStore.AddMessage(IMessage message, bool appendOnStart)
    {
        lock (messages)
        {
            if (appendOnStart)
            {
                messages.Insert(0, message);
            }
            else
            {
                messages.Add(message);
            }
        }
    }

    IMessage? IMessageStore.GetMessageById(Guid id)
        => messages.FirstOrDefault(p => p.Id == id);

    void IMessageStore.UpdateMessage(Guid id, IMessage message)
    {
        lock (messages)
        {
            var index = messages.FindIndex(p => p.Id == id);
            if (index >= 0)
            {
                messages[index] = message;
            }
        }
    }

    int IMessageStore.GetMessageCount()
    {
        lock (messages)
        {
            return messages.Count;
        }
    }

    IEnumerable<IMessage> IMessageStore.GetRecentHistory(int pageSize)
    {
        lock (messages)
        {
            return messages
                .AsEnumerable()
                .Reverse()
                .Take(pageSize)
                .Reverse()
                .ToList();
        }
    }

    IEnumerable<IMessage> IMessageStore.GetHistory(int? page, int? pageSize)
    {
        lock (messages)
        {
            var resultMessages = messages.AsEnumerable();

            if (page.HasValue && pageSize.HasValue)
            {
                resultMessages = messages.Skip(page.Value * pageSize.Value).Take((int)pageSize);
            }

            return resultMessages;
        }
    }

    IEnumerable<IMessage> IMessageStore.GetMessagesOlderThan(DateTime? oldestLoadedTimestamp, Guid? oldestLoadedMessageId, int pageSize)
    {
        lock (messages)
        {
            IEnumerable<IMessage> query = messages;

            if (oldestLoadedTimestamp.HasValue)
            {
                query = query.Where(m => IsOlderThanAnchor(m, oldestLoadedTimestamp.Value, oldestLoadedMessageId));
            }

            return query
                .Reverse()
                .Take(pageSize)
                .ToList();
        }
    }

    private static bool IsOlderThanAnchor(IMessage m, DateTime anchorTimestamp, Guid? anchorId)
    {
        if (m.Timestamp != anchorTimestamp)
        {
            return m.Timestamp < anchorTimestamp;
        }

        // Тай-брейк при равных Timestamp: Guid как таковой не даёт хронологического порядка,
        // но обеспечивает детерминированность, чтобы не потерять и не задублировать сообщения
        // с одинаковой меткой времени.
        return anchorId.HasValue && m.Id.CompareTo(anchorId.Value) < 0;
    }

    IEnumerable<IMessage> IMessageStore.GetMessagesNewerThan(DateTime? latestLoadedTimestamp, Guid? latestLoadedMessageId, int pageSize)
    {
        lock (messages)
        {
            IEnumerable<IMessage> query = messages;

            if (latestLoadedTimestamp.HasValue)
            {
                query = query.Where(m => IsNewerThanAnchor(m, latestLoadedTimestamp.Value, latestLoadedMessageId));
            }

            return query
                .Take(pageSize)
                .ToList();
        }
    }

    private static bool IsNewerThanAnchor(IMessage m, DateTime anchorTimestamp, Guid? anchorId)
    {
        if (m.Timestamp != anchorTimestamp)
        {
            return m.Timestamp > anchorTimestamp;
        }

        // Тай-брейк при равных Timestamp: Guid как таковой не даёт хронологического порядка,
        // но обеспечивает детерминированность, чтобы не потерять и не задублировать сообщения
        // с одинаковой меткой времени.
        return anchorId.HasValue && m.Id.CompareTo(anchorId.Value) > 0;
    }

    IMessage? IMessageStore.GetLastMessage()
    {
        lock (messages)
        {
            return messages.LastOrDefault();
        }
    }

    IMessage? IMessageStore.GetFirstMessage()
    {
        lock (messages)
        {
            return messages.FirstOrDefault();
        }
    }

    void IMessageStore.RemoveMessage(Guid id)
    {
        lock (messages)
        {
            messages.RemoveAll(x => x.Id == id);
        }
    }

    void IMessageStore.ClearMessages()
    {
        lock (messages)
        {
            messages.Clear();
        }
    }
}
