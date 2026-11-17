using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Services;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="RoomStore"/>
/// </summary>
public class RoomStoreTests : IDisposable
{
    private readonly TestRoomFactory roomFactory = new();
    private readonly IRoomStore store;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomStoreTests"/>
    /// </summary>
    public RoomStoreTests()
    {
        store = new RoomStore(roomFactory.Factory);
    }

    /// <summary>
    /// Создать комнату с хостом
    /// </summary>
    private static Room CreateRoom(string name = "Комната")
        => new()
        {
            Name = name,
            HostParticipant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Хост" },
            MaximumParticipants = 10,
        };

    /// <summary>
    /// Регистрация, поиск и удаление комнаты работают
    /// </summary>
    [Fact]
    public void RegisterGetRemoveShouldWork()
    {
        // Arrange
        var room = CreateRoom();

        // Act
        store.Register(room);
        var exists = store.RoomExists(room.Id);
        var found = store.GetRoom(room.Id);
        var tryFound = store.TryGetRoom(room.Id, out var tried);
        store.Remove(room.Id);

        // Assert
        exists.Should().BeTrue();
        found.Should().BeSameAs(room);
        tryFound.Should().BeTrue();
        tried.Should().BeSameAs(room);
        store.RoomExists(room.Id).Should().BeFalse();
        store.TryGetRoom(room.Id, out _).Should().BeFalse();
    }

    /// <summary>
    /// Неизвестная комната: GetRoom бросает InvalidOperationException
    /// </summary>
    [Fact]
    public void GetRoomUnknownShouldThrow()
    {
        // Act
        var act = () => store.GetRoom(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// GetAllRooms возвращает все зарегистрированные комнаты
    /// </summary>
    [Fact]
    public void GetAllRoomsShouldReturnAll()
    {
        // Arrange
        store.Register(CreateRoom("Первая"));
        store.Register(CreateRoom("Вторая"));

        // Act
        var all = store.GetAllRooms().ToList();

        // Assert
        all.Should().HaveCount(2);
    }

    /// <summary>
    /// Регистрация привязывает список участников к контексту комнаты
    /// </summary>
    [Fact]
    public void RegisterShouldBindParticipantsToContext()
    {
        // Arrange
        var room = CreateRoom();
        var participant = new Participant { Name = "Участник" };
        room.CurrentParticipants = [participant];

        // Act
        store.Register(room);
        var context = roomFactory.Factory.GetOrCreateContext(room.Id);

        // Assert
        context.Participants.GetParticipants().Should().ContainSingle().Which.Id.Should().Be(participant.Id);
    }

    /// <summary>
    /// GetRoomFor отдаёт только видимые участнику сообщения
    /// </summary>
    [Fact]
    public void GetRoomForShouldSanitizePrivateMessages()
    {
        // Arrange
        var room = CreateRoom();
        store.Register(room);
        var context = roomFactory.Factory.GetOrCreateContext(room.Id);
        var participantId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        context.Messages.AddMessage(new ChatTextMessage { Content = "публичное", SenderId = strangerId });
        context.Messages.AddMessage(new ChatTextMessage { Content = "личное мне", SenderId = strangerId, RecipientId = participantId });
        context.Messages.AddMessage(new ChatTextMessage { Content = "моё личное", SenderId = participantId, RecipientId = strangerId });
        context.Messages.AddMessage(new ChatTextMessage { Content = "чужое личное", SenderId = strangerId, RecipientId = Guid.NewGuid() });

        // Act
        var snapshot = store.GetRoomFor(participantId, room.Id);

        // Assert
        snapshot.ChatHistory.Should().HaveCount(3);
        snapshot.ChatHistory.Cast<ChatTextMessage>().Select(m => m.Content)
            .Should().Equal("публичное", "личное мне", "моё личное");
        snapshot.TotalMessageCount.Should().Be(3);
    }

    /// <summary>
    /// GetRoomFor при asRejoin не подмешивает историю из контекста
    /// </summary>
    [Fact]
    public void GetRoomForAsRejoinShouldKeepRoomHistory()
    {
        // Arrange
        var room = CreateRoom();
        room.ChatHistory = [new ChatTextMessage { Content = "из комнаты" }];
        store.Register(room);
        var context = roomFactory.Factory.GetOrCreateContext(room.Id);
        context.Messages.AddMessage(new ChatTextMessage { Content = "из контекста" });

        // Act
        var snapshot = store.GetRoomFor(Guid.NewGuid(), room.Id, asRejoin: true);

        // Assert
        snapshot.ChatHistory.Should().HaveCount(1);
        ((ChatTextMessage)snapshot.ChatHistory[0]).Content.Should().Be("из комнаты");
    }

    /// <summary>
    /// GetRoomChatHistoryCountFor считает только видимые сообщения
    /// </summary>
    [Fact]
    public void GetRoomChatHistoryCountForShouldCountVisible()
    {
        // Arrange
        var room = CreateRoom();
        store.Register(room);
        var context = roomFactory.Factory.GetOrCreateContext(room.Id);
        var participantId = Guid.NewGuid();
        context.Messages.AddMessage(new ChatTextMessage { Content = "публичное" });
        context.Messages.AddMessage(new ChatTextMessage { Content = "чужое личное", RecipientId = Guid.NewGuid() });

        // Act
        var count = store.GetRoomChatHistoryCountFor(participantId, room.Id);

        // Assert
        count.Should().Be(1);
    }

    /// <summary>
    /// GetRoomHostParticipantId возвращает id хоста, для неизвестной комнаты бросает KeyNotFoundException
    /// </summary>
    [Fact]
    public void GetRoomHostParticipantIdShouldWork()
    {
        // Arrange
        var room = CreateRoom();
        store.Register(room);

        // Act
        var hostId = store.GetRoomHostParticipantId(room.Id);
        var act = () => store.GetRoomHostParticipantId(Guid.NewGuid());

        // Assert
        hostId.Should().Be(room.HostParticipant.Id);
        act.Should().Throw<KeyNotFoundException>();
    }

    /// <inheritdoc />
    public void Dispose() => roomFactory.Dispose();
}
