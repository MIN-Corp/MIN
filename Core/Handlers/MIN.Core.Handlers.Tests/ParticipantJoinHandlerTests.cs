using FluentAssertions;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Stateless.RoomRelated.Join;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Handlers.Tests;

/// <summary>
/// Тесты на ParticipantJoinHandler
/// </summary>
public class ParticipantJoinHandlerTests : IDisposable
{
    private readonly HandlerTestFactory factory = new();
    private readonly Guid roomId = Guid.NewGuid();

    /// <summary>
    /// Контекст входящего join-запроса от клиента
    /// </summary>
    private MessageContext CreateJoinContext(Guid senderId)
    {
        var roomContext = factory.RoomFactory.GetOrCreateContext(roomId);
        return TestMessageContext.Create(roomContext, senderId, Guid.NewGuid(), Role.Host);
    }

     /// <summary>
    /// Зарегистрировать комнату с хостом
    /// </summary>
    private void RegisterRoom()
    {
        factory.RoomStore.Register(new Room
        {
            Id = roomId,
            Name = "Комната",
            HostParticipant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Хост" },
            MaximumParticipants = 10,
        });
    }

    /// <summary>
    /// Контекст входящего join-запроса с фиксированным соединением
    /// </summary>
    private (MessageContext Context, Guid ConnectionId) CreateJoinContextWithConnection(Guid senderId)
    {
        var connectionId = Guid.NewGuid();
        var roomContext = factory.RoomFactory.GetOrCreateContext(roomId);
        return (TestMessageContext.Create(roomContext, senderId, connectionId, Role.Host), connectionId);
    }

    /// <summary>
    /// Забаненный участник отклоняется с сообщением о бане
    /// </summary>
    [Fact]
    public async Task BannedJoinerShouldBeRejectedWithBanMessage()
    {
        // Arrange
        RegisterRoom();
        var senderId = Guid.NewGuid();
        var room = factory.RoomStore.GetRoom(roomId);
        room.LocalRoomSettings.BannedParticipants[senderId] =
            new BanInfo(senderId, "Вася", "спам", DateTimeOffset.Now.AddDays(3));

        var handler = factory.GetHandler(MessageTypeTag.RoomJoinRequest);
        var message = new RoomJoinRequestMessage { SenderId = senderId };

        // Act
        var result = await handler.HandleAsync(message, CreateJoinContext(senderId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Вы забанены до");
        result.CriticalErrorForConnection.Should().BeTrue();
    }

    /// <summary>
    /// Вечный бан отклоняется сообщением о вечном бане
    /// </summary>
    [Fact]
    public async Task PermanentlyBannedJoinerShouldBeRejectedWithPermanentMessage()
    {
        // Arrange
        RegisterRoom();
        var senderId = Guid.NewGuid();
        var room = factory.RoomStore.GetRoom(roomId);
        room.LocalRoomSettings.BannedParticipants[senderId] =
            new BanInfo(senderId, "Вася", "", null);

        var handler = factory.GetHandler(MessageTypeTag.RoomJoinRequest);
        var message = new RoomJoinRequestMessage { SenderId = senderId };

        // Act
        var result = await handler.HandleAsync(message, CreateJoinContext(senderId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().Be("Вы забанены навсегда");
    }

    /// <summary>
    /// Истёкший или отсутствующий бан не блокирует вход
    /// </summary>
    [Fact]
    public async Task ExpiredBanShouldNotBlockJoin()
    {
        // Arrange
        RegisterRoom();
        var senderId = Guid.NewGuid();
        var room = factory.RoomStore.GetRoom(roomId);
        room.LocalRoomSettings.BannedParticipants[senderId] =
            new BanInfo(senderId, "Вася", "спам", DateTimeOffset.Now.AddSeconds(-1));

        var handler = factory.GetHandler(MessageTypeTag.RoomJoinRequest);
        var message = new RoomJoinRequestMessage { SenderId = senderId };

        // Act
        var result = await handler.HandleAsync(message, CreateJoinContext(senderId));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Бан имеет приоритет над ожидающим киком и не расходует его
    /// </summary>
    [Fact]
    public async Task BanShouldTakePrecedenceOverPendingKick()
    {
        // Arrange
        RegisterRoom();
        var senderId = Guid.NewGuid();
        var room = factory.RoomStore.GetRoom(roomId);
        room.LocalRoomSettings.PendingKickParticipantIds[senderId] = "старый кик";
        room.LocalRoomSettings.BannedParticipants[senderId] =
            new BanInfo(senderId, "Вася", "", null);

        var handler = factory.GetHandler(MessageTypeTag.RoomJoinRequest);
        var message = new RoomJoinRequestMessage { SenderId = senderId };

        // Act
        var result = await handler.HandleAsync(message, CreateJoinContext(senderId));

        // Assert
        result.ErrorMessage.Should().Be("Вы забанены навсегда");
        room.LocalRoomSettings.PendingKickParticipantIds.Should().ContainKey(senderId);
    }

    /// <summary>
    /// Новый участник с забаненным IP отклоняется, даже если его Id не в бане
    /// </summary>
    [Fact]
    public async Task BannedByIpJoinerShouldBeRejectedWithBanMessage()
    {
        // Arrange
        RegisterRoom();
        var senderId = Guid.NewGuid();
        var (context, connectionId) = CreateJoinContextWithConnection(senderId);
        factory.RoomFactory.GetOrCreateContext(roomId).Connections.RegisterConnectionIp(connectionId, "10.0.0.1");

        var room = factory.RoomStore.GetRoom(roomId);
        room.LocalRoomSettings.BannedParticipants[Guid.NewGuid()] =
            new BanInfo(Guid.NewGuid(), "Вася", "спам", DateTimeOffset.Now.AddDays(3), "10.0.0.1", IsIpBanned: true);

        var handler = factory.GetHandler(MessageTypeTag.RoomJoinRequest);
        var message = new RoomJoinRequestMessage { SenderId = senderId };

        // Act
        var result = await handler.HandleAsync(message, context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Вы забанены до");
        result.CriticalErrorForConnection.Should().BeTrue();
    }

    /// <summary>
    /// Известный IP с выключенным флагом не блокирует вход
    /// </summary>
    [Fact]
    public async Task DisabledIpBanShouldNotBlockJoin()
    {
        // Arrange
        RegisterRoom();
        var senderId = Guid.NewGuid();
        var (context, connectionId) = CreateJoinContextWithConnection(senderId);
        factory.RoomFactory.GetOrCreateContext(roomId).Connections.RegisterConnectionIp(connectionId, "10.0.0.1");

        var room = factory.RoomStore.GetRoom(roomId);
        room.LocalRoomSettings.BannedParticipants[Guid.NewGuid()] =
            new BanInfo(Guid.NewGuid(), "Вася", "спам", DateTimeOffset.Now.AddDays(1), "10.0.0.1", IsIpBanned: false);

        var handler = factory.GetHandler(MessageTypeTag.RoomJoinRequest);
        var message = new RoomJoinRequestMessage { SenderId = senderId };

        // Act
        var result = await handler.HandleAsync(message, context);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => factory.Dispose();
}
