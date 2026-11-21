using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Models.Persistence;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Registries;
using MIN.Core.Stores.Services;
using MIN.Core.Stores.Services.Persistence;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="RoomSnapshotMapper"/>
/// </summary>
public class RoomSnapshotMapperTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly TestLoggerProvider logger = new();
    private readonly TestIdentityService identity = new();
    private readonly IRoomStore roomStore;
    private readonly IRoomConnectionRegistry registry = new RoomConnectionRegistry();
    private readonly IRoomSnapshotMapper mapper;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomSnapshotMapperTests"/>
    /// </summary>
    public RoomSnapshotMapperTests()
    {
        roomStore = new RoomStore(rooms.Factory);
        mapper = new RoomSnapshotMapper(roomStore, registry, identity, rooms.Factory, logger);
    }

    /// <summary>
    /// Снапшот клонирует комнату и сохраняет сообщения с подкомнатами
    /// </summary>
    [Fact]
    public void BuildSnapshotShouldCloneRoomAndMessages()
    {
        // Arrange
        var room = new Room { Id = Guid.NewGuid(), Name = "Комната", HostParticipant = new ParticipantInfo { Id = Guid.NewGuid() } };
        roomStore.Register(room);
        var context = rooms.Factory.GetOrCreateContext(room.Id);
        context.Messages.AddMessage(new ChatTextMessage { Content = "сообщение" });
        context.SubRooms.HostSubRoom(new ParticipantInfo { Id = Guid.NewGuid() }, Contracts.Enums.SubRoomPurpose.Activity, 4);

        // Act
        var snapshot = mapper.BuildSnapshot(room.Id);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.Room.Should().NotBeSameAs(room);
        snapshot.Room.Id.Should().Be(room.Id);
        snapshot.Messages.Should().HaveCount(1);
        snapshot.SubRooms!.SubRooms.Should().HaveCount(1);
    }

    /// <summary>
    /// Снапшот неизвестной комнаты — null
    /// </summary>
    [Fact]
    public void BuildSnapshotUnknownRoomShouldReturnNull()
    {
        // Act
        var snapshot = mapper.BuildSnapshot(Guid.NewGuid());

        // Assert
        snapshot.Should().BeNull();
    }

    /// <summary>
    /// Восстановление своей комнаты регистрирует её офлайн с сообщениями
    /// </summary>
    [Fact]
    public void RestoreSnapshotShouldRegisterOfflineRoom()
    {
        // Arrange
        var selfId = identity.SelfParticipant.Id;
        var room = new Room { Id = Guid.NewGuid(), Name = "Моя комната", HostParticipant = new ParticipantInfo { Id = selfId } };
        var participant = new Participant { Id = Guid.NewGuid(), CurrentStatus = Entities.Contracts.Enums.OnlineStatus.Online };
        room.CurrentParticipants = [participant];
        var snapshot = new RoomSnapshot
        {
            Room = room,
            Messages = [new ChatTextMessage { Content = "восстановленное" }],
        };

        // Act
        mapper.RestoreSnapshot(snapshot);

        // Assert
        roomStore.RoomExists(room.Id).Should().BeTrue();
        roomStore.GetRoom(room.Id).IsOnline.Should().BeFalse();
        registry.GetRole(room.Id).Should().Be(Entities.Contracts.Enums.Role.Host);
        var context = rooms.Factory.GetOrCreateContext(room.Id);
        context.Messages.GetMessageCount().Should().Be(1);
        context.Participants.GetParticipants().Should().OnlyContain(p => p.CurrentStatus == Entities.Contracts.Enums.OnlineStatus.Offline);
    }

    /// <summary>
    /// Восстановление чужой комнаты не регистрирует серверное соединение
    /// </summary>
    [Fact]
    public void RestoreSnapshotForeignHostShouldNotRegisterServer()
    {
        // Arrange
        var room = new Room { Id = Guid.NewGuid(), Name = "Чужая", HostParticipant = new ParticipantInfo { Id = Guid.NewGuid() } };
        var snapshot = new RoomSnapshot { Room = room, Messages = [] };

        // Act
        mapper.RestoreSnapshot(snapshot);

        // Assert
        roomStore.RoomExists(room.Id).Should().BeTrue();
        registry.IsHosting(room.Id).Should().BeFalse();
    }

    /// <summary>
    /// Снапшот сохраняет хеш пароля из локальных настроек
    /// </summary>
    [Fact]
    public void BuildSnapshotShouldPreservePasswordHash()
    {
        // Arrange
        var room = new Room
        {
            Id = Guid.NewGuid(), Name = "Комната", HostParticipant = new ParticipantInfo { Id = Guid.NewGuid() }, LocalRoomSettings =
            {
                PasswordHash = "600000:AbCdEf=="
            }
        };
        roomStore.Register(room);

        // Act
        var snapshot = mapper.BuildSnapshot(room.Id);

        // Assert
        snapshot!.Room.LocalRoomSettings.PasswordHash.Should().Be("600000:AbCdEf==");
    }

    /// <summary>
    /// Снапшот сохраняет признак защищённости комнаты паролем
    /// </summary>
    [Fact]
    public void BuildSnapshotShouldPreserveHasPassword()
    {
        // Arrange
        var room = new Room { Id = Guid.NewGuid(), Name = "Комната", HostParticipant = new ParticipantInfo { Id = Guid.NewGuid() } };
        room.HasPassword = true;
        roomStore.Register(room);

        // Act
        var snapshot = mapper.BuildSnapshot(room.Id);

        // Assert
        snapshot!.Room.HasPassword.Should().BeTrue();
    }

    /// <inheritdoc />
    public void Dispose() => rooms.Dispose();
}
