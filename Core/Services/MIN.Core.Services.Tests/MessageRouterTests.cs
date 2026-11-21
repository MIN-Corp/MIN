using FluentAssertions;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Services;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Events;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Services.Messaging;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Registries;
using MIN.Core.Stores.Services;
using MIN.Tests.Common;
using Moq;
using MIN.Core.Stores.Contracts.Interfaces;
using Xunit;

namespace MIN.Core.Services.Tests;

/// <summary>
/// Тесты на <see cref="MessageRouter"/>
/// </summary>
public class MessageRouterTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly TestLoggerProvider logger = new();
    private readonly InMemoryEventBus eventBus;
    private readonly IRoomConnectionRegistry registry = new RoomConnectionRegistry();
    private readonly Mock<IMessageSender> sender = new();
    private readonly IRoomStore roomStore;
    private readonly IMessageRouter router;
    private readonly Guid roomId = Guid.NewGuid();
    private readonly Guid senderId = Guid.NewGuid();

    /// <summary>
    /// Сообщение с локальным дублированием
    /// </summary>
    private sealed class DuplicationMessage : IMessage
    {
        /// <inheritdoc />
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <inheritdoc />
        public DateTime Timestamp { get; init; } = DateTime.Now;

        /// <inheritdoc />
        public MessageTypeTag TypeTag => MessageTypeTag.ChatTextMessage;

        /// <inheritdoc />
        public Guid SenderId { get; set; }

        /// <inheritdoc />
        public Guid? RecipientId { get; init; }

        /// <inheritdoc />
        public bool IsPublic => RecipientId == null;

        /// <inheritdoc />
        public bool RequiresLocalDuplication => true;

        /// <inheritdoc />
        public bool RequiresEncryption => true;

        /// <inheritdoc />
        public bool RequireStreamAcks => false;

        /// <inheritdoc />
        public MessageChannel Channel => MessageChannel.Secure;
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MessageRouterTests"/>
    /// </summary>
    public MessageRouterTests()
    {
        eventBus = new InMemoryEventBus(logger);
        roomStore = new RoomStore(rooms.Factory);
        router = new MessageRouter(registry, roomStore, eventBus, sender.Object, rooms.Factory);
    }

    /// <summary>
    /// Хост маршрутизирует сообщение локально
    /// </summary>
    [Fact]
    public async Task HostRouteShouldPublishLocally()
    {
        // Arrange
        using var capture = new EventCapture<LocalMessageReceivedEvent>(eventBus);
        registry.RegisterServerConnection(roomId, Guid.NewGuid());
        var message = new MIN.Chat.Messaging.ChatTextMessage { Content = "от хоста" };

        // Act
        await router.RouteAsync(message, roomId, senderId, CancellationToken.None);

        // Assert
        await capture.WaitAsync();
        var local = capture.Events.Single();
        local.Message.Should().BeSameAs(message);
        local.Role.Should().Be(Role.Host);
        message.SenderId.Should().Be(senderId);
        sender.Verify(s => s.SendAsync(It.IsAny<IMessage>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Клиент отправляет сообщение хосту
    /// </summary>
    [Fact]
    public async Task ClientRouteShouldSendToHost()
    {
        // Arrange
        var hostId = Guid.NewGuid();
        var hostConnection = Guid.NewGuid();
        registry.RegisterClientConnection(roomId, Guid.NewGuid());
        roomStore.Register(new Room { Id = roomId, Name = "Комната", HostParticipant = new ParticipantInfo { Id = hostId } });
        rooms.Factory.GetOrCreateContext(roomId).Connections.Register(hostConnection, new ParticipantInfo { Id = hostId });
        var message = new MIN.Chat.Messaging.ChatTextMessage { Content = "хосту" };

        // Act
        await router.RouteAsync(message, roomId, senderId, CancellationToken.None);

        // Assert
        sender.Verify(s => s.SendAsync(message, roomId, hostConnection, It.IsAny<CancellationToken>()), Times.Once);
        message.SenderId.Should().Be(senderId);
    }

    /// <summary>
    /// Клиент с локальным дублированием публикует и отправляет
    /// </summary>
    [Fact]
    public async Task ClientRouteWithLocalDuplicationShouldPublishAndSend()
    {
        // Arrange
        using var capture = new EventCapture<LocalMessageReceivedEvent>(eventBus);
        var hostId = Guid.NewGuid();
        var hostConnection = Guid.NewGuid();
        registry.RegisterClientConnection(roomId, Guid.NewGuid());
        roomStore.Register(new Room { Id = roomId, Name = "Комната", HostParticipant = new ParticipantInfo { Id = hostId } });
        rooms.Factory.GetOrCreateContext(roomId).Connections.Register(hostConnection, new ParticipantInfo { Id = hostId });
        var message = new DuplicationMessage();

        // Act
        await router.RouteAsync(message, roomId, senderId, CancellationToken.None);

        // Assert
        await capture.WaitAsync();
        capture.Events.Single().Message.Should().BeSameAs(message);
        sender.Verify(s => s.SendAsync(message, roomId, hostConnection, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Неизвестная комната отклоняется
    /// </summary>
    [Fact]
    public async Task UnknownRoomShouldThrow()
    {
        // Act
        var act = () => router.RouteAsync(new MIN.Chat.Messaging.ChatTextMessage(), Guid.NewGuid(), senderId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<MIN.Core.Stores.Contracts.Exceptions.RoomNotRegisteredException>();
    }

    /// <summary>
    /// Клиент без соединения хоста получает InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ClientWithoutHostConnectionShouldThrow()
    {
        // Arrange
        var hostId = Guid.NewGuid();
        registry.RegisterClientConnection(roomId, Guid.NewGuid());
        roomStore.Register(new Room { Id = roomId, Name = "Комната", HostParticipant = new ParticipantInfo { Id = hostId } });

        // Act
        var act = () => router.RouteAsync(new MIN.Chat.Messaging.ChatTextMessage(), roomId, senderId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not registered*");
    }

    /// <summary>
    /// PublishLocally несёт роль и исключения рассылки
    /// </summary>
    [Fact]
    public async Task PublishLocallyShouldCarryRoleAndExcludes()
    {
        // Arrange
        using var capture = new EventCapture<LocalMessageReceivedEvent>(eventBus);
        registry.RegisterServerConnection(roomId, Guid.NewGuid());
        var excludes = new[] { Guid.NewGuid() };
        var message = new MIN.Chat.Messaging.ChatTextMessage();

        // Act
        await router.PublishLocally(message, roomId, excludes, CancellationToken.None);

        // Assert
        await capture.WaitAsync();
        capture.Events.Single().Role.Should().Be(Role.Host);
        capture.Events.Single().BroadcastExcludeIds.Should().BeEquivalentTo(excludes);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        rooms.Dispose();
    }
}
