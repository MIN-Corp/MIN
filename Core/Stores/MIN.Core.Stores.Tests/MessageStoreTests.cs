using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Core.Messaging.Stateless.RoomRelated.Ping;
using MIN.Core.Stores.Contracts.Constants;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Services;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="MessageStore"/>
/// </summary>
public class MessageStoreTests
{
    private readonly IMessageStore store = new MessageStore();

    /// <summary>
    /// Создать текстовое сообщение с фиксированной меткой времени
    /// </summary>
    private static ChatTextMessage Text(string content, DateTime? timestamp = null, Guid? senderId = null, Guid? recipientId = null)
        => new()
        {
            Content = content,
            Timestamp = timestamp ?? new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            SenderId = senderId ?? Guid.Empty,
            RecipientId = recipientId,
        };

    /// <summary>
    /// Сообщения добавляются в конец, appendOnStart — в начало
    /// </summary>
    [Fact]
    public void AddMessageShouldAppendAndPrepend()
    {
        // Arrange
        var first = Text("первое");
        var second = Text("второе");
        var zero = Text("нулевое");

        // Act
        store.AddMessage(first);
        store.AddMessage(second);
        store.AddMessage(zero, appendOnStart: true);

        // Assert
        store.GetMessageCount().Should().Be(3);
        store.GetFirstMessage().Should().BeSameAs(zero);
        store.GetLastMessage().Should().BeSameAs(second);
    }

    /// <summary>
    /// Поиск по идентификатору находит сообщение, неизвестный id даёт null
    /// </summary>
    [Fact]
    public void GetMessageByIdShouldWork()
    {
        // Arrange
        var message = Text("сообщение");
        store.AddMessage(message);

        // Act
        var found = store.GetMessageById(message.Id);
        var missing = store.GetMessageById(Guid.NewGuid());

        // Assert
        found.Should().BeSameAs(message);
        missing.Should().BeNull();
    }

    /// <summary>
    /// Обновление меняет контент и помечает сообщение обновлённым
    /// </summary>
    [Fact]
    public void UpdateMessageShouldUpdateContent()
    {
        // Arrange
        var message = Text("старый");
        store.AddMessage(message);

        // Act
        store.UpdateMessage(message.Id, Text("новый"));

        // Assert
        var updated = (ChatTextMessage)store.GetMessageById(message.Id)!;
        updated.Content.Should().Be("новый");
        updated.IsUpdated.Should().BeTrue();
    }

    /// <summary>
    /// Обновление неизвестного сообщения — no-op
    /// </summary>
    [Fact]
    public void UpdateUnknownMessageShouldBeNoOp()
    {
        // Arrange
        store.AddMessage(Text("единственное"));

        // Act
        var act = () => store.UpdateMessage(Guid.NewGuid(), Text("чужое"));

        // Assert
        act.Should().NotThrow();
        store.GetMessageCount().Should().Be(1);
    }

    /// <summary>
    /// Обновление необновляемого сообщения — no-op
    /// </summary>
    [Fact]
    public void UpdateNonUpdateableMessageShouldBeNoOp()
    {
        // Arrange
        var ping = new PingMessage();
        store.AddMessage(ping);

        // Act
        store.UpdateMessage(ping.Id, Text("не применяется"));

        // Assert
        store.GetMessageById(ping.Id).Should().BeSameAs(ping);
    }

    /// <summary>
    /// GetRecentHistory возвращает последние N в хронологическом порядке
    /// </summary>
    [Fact]
    public void GetRecentHistoryShouldReturnLastNChronologically()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
        {
            store.AddMessage(Text($"сообщение {i}"));
        }

        // Act
        var recent = store.GetRecentHistory(3).ToList();

        // Assert
        recent.Should().HaveCount(3);
        recent.Select(m => ((ChatTextMessage)m).Content).Should().Equal("сообщение 7", "сообщение 8", "сообщение 9");
    }

    /// <summary>
    /// Размер страницы по умолчанию равен StoreConstants.MessagesPageSize
    /// </summary>
    [Fact]
    public void GetRecentHistoryShouldUseDefaultPageSize()
    {
        // Arrange
        for (var i = 0; i < StoreConstants.MessagesPageSize + 10; i++)
        {
            store.AddMessage(Text($"сообщение {i}"));
        }

        // Act
        var recent = store.GetRecentHistory().ToList();

        // Assert
        recent.Should().HaveCount(StoreConstants.MessagesPageSize);
    }

    /// <summary>
    /// Пагинация GetHistory пропускает page * pageSize сообщений
    /// </summary>
    [Fact]
    public void GetHistoryShouldPaginate()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
        {
            store.AddMessage(Text($"сообщение {i}"));
        }

        // Act
        var page0 = store.GetHistory(0, 4).ToList();
        var page1 = store.GetHistory(1, 4).ToList();
        var page2 = store.GetHistory(2, 4).ToList();

        // Assert
        page0.Should().HaveCount(4);
        page1.Should().HaveCount(4);
        page2.Should().HaveCount(2);
        ((ChatTextMessage)page1[0]).Content.Should().Be("сообщение 4");
    }

    /// <summary>
    /// GetMessagesOlderThan возвращает сообщения строго старше якоря, новые первыми
    /// </summary>
    [Fact]
    public void GetMessagesOlderThanShouldFilterAndOrder()
    {
        // Arrange
        var anchorTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var older1 = Text("старое 1", anchorTime.AddMinutes(-2));
        var older2 = Text("старое 2", anchorTime.AddMinutes(-1));
        var anchor = Text("якорь", anchorTime);
        var newer = Text("новое", anchorTime.AddMinutes(1));
        store.AddMessage(older1);
        store.AddMessage(older2);
        store.AddMessage(anchor);
        store.AddMessage(newer);

        // Act
        var older = store.GetMessagesOlderThan(anchorTime, anchor.Id, 10).ToList();

        // Assert
        older.Should().HaveCount(2);
        older[0].Should().BeSameAs(older2);
        older[1].Should().BeSameAs(older1);
    }

    /// <summary>
    /// При равных Timestamp тай-брейк по Guid детерминирован
    /// </summary>
    [Fact]
    public void GetMessagesOlderThanShouldBreakTiesByGuid()
    {
        // Arrange
        var time = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var smallGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var bigGuid = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var withSmallId = Text("с малым id", time);
        withSmallId.Id = smallGuid;
        var withBigId = Text("с большим id", time);
        withBigId.Id = bigGuid;
        store.AddMessage(withSmallId);
        store.AddMessage(withBigId);

        // Act
        var olderThanBig = store.GetMessagesOlderThan(time, bigGuid, 10).ToList();
        var olderThanSmall = store.GetMessagesOlderThan(time, smallGuid, 10).ToList();

        // Assert
        olderThanBig.Should().ContainSingle().Which.Should().BeSameAs(withSmallId);
        olderThanSmall.Should().BeEmpty();
    }

    /// <summary>
    /// GetMessagesNewerThan возвращает сообщения строго новее якоря
    /// </summary>
    [Fact]
    public void GetMessagesNewerThanShouldFilter()
    {
        // Arrange
        var anchorTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var older = Text("старое", anchorTime.AddMinutes(-1));
        var anchor = Text("якорь", anchorTime);
        var newer1 = Text("новое 1", anchorTime.AddMinutes(1));
        var newer2 = Text("новое 2", anchorTime.AddMinutes(2));
        store.AddMessage(older);
        store.AddMessage(anchor);
        store.AddMessage(newer1);
        store.AddMessage(newer2);

        // Act
        var newer = store.GetMessagesNewerThan(anchorTime, anchor.Id, 10).ToList();
        var limited = store.GetMessagesNewerThan(anchorTime, anchor.Id, 1).ToList();

        // Assert
        newer.Should().HaveCount(2);
        newer[0].Should().BeSameAs(newer1);
        limited.Should().HaveCount(1);
    }

    /// <summary>
    /// Удаление и очистка работают
    /// </summary>
    [Fact]
    public void RemoveAndClearShouldWork()
    {
        // Arrange
        var first = Text("первое");
        var second = Text("второе");
        store.AddMessage(first);
        store.AddMessage(second);

        // Act
        store.RemoveMessage(first.Id);
        var countAfterRemove = store.GetMessageCount();
        store.ClearMessages();

        // Assert
        countAfterRemove.Should().Be(1);
        store.GetMessageById(first.Id).Should().BeNull();
        store.GetMessageCount().Should().Be(0);
        store.GetLastMessage().Should().BeNull();
        store.GetFirstMessage().Should().BeNull();
    }

    /// <summary>
    /// Параллельные добавления не теряют сообщения
    /// </summary>
    [Fact]
    public void ConcurrentAddsShouldNotLoseMessages()
    {
        // Act
        Parallel.For(0, 10, batch =>
        {
            for (var i = 0; i < 100; i++)
            {
                store.AddMessage(Text($"сообщение {batch}-{i}"));
            }
        });

        // Assert
        store.GetMessageCount().Should().Be(1000);
    }
}
