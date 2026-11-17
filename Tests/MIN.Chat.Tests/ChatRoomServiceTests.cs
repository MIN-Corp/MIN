using FluentAssertions;
using MIN.Chat.Services;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.History;
using MIN.Core.Messaging.Stateless.RoomRelated.RoomInfo;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Registries;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;
using MIN.Discovery.Services.Contracts.Interfaces;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Chat.Tests;

/// <summary>
/// Тесты на <see cref="ChatRoomService"/>
/// </summary>
public class ChatRoomServiceTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly IRoomConnectionRegistry registry = new RoomConnectionRegistry();
    private readonly Mock<IMessageRouter> router = new();
    private readonly Mock<IRoomLifecycleManager> lifecycle = new();
    private readonly Mock<INetworkErrorHandler> errorHandler = new();
    private readonly Mock<IDiscoveryService> discovery = new();
    private readonly TestIdentityService identity = new();
    private readonly IChatRoomService service;
    private readonly Guid roomId = Guid.NewGuid();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatRoomServiceTests"/>
    /// </summary>
    public ChatRoomServiceTests()
    {
        service = new ChatRoomService(router.Object, registry, lifecycle.Object, rooms.Factory, errorHandler.Object, discovery.Object, identity);
    }

    /// <summary>
    /// Кик не хостом отклоняется
    /// </summary>
    [Fact]
    public async Task KickWhenNotHostingShouldThrow()
    {
        // Act
        var act = () => service.KickParticipantAsync(roomId, Guid.NewGuid(), "причина", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Кик оффлайн-участника идёт напрямую через lifecycle
    /// </summary>
    [Fact]
    public async Task KickOfflineParticipantShouldKickDirectly()
    {
        // Arrange
        registry.RegisterServerConnection(roomId, Guid.NewGuid());
        var participantId = Guid.NewGuid();

        // Act
        await service.KickParticipantAsync(roomId, participantId, "причина", CancellationToken.None);

        // Assert
        lifecycle.Verify(l => l.KickClientAsync(roomId, participantId, DisconnectReason.Kick, "причина"), Times.Once);
        errorHandler.Verify(e => e.SendErrorAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>
    /// Кик онлайн-участника отправляет критичную ошибку
    /// </summary>
    [Fact]
    public async Task KickOnlineParticipantShouldSendCriticalError()
    {
        // Arrange
        registry.RegisterServerConnection(roomId, Guid.NewGuid());
        var participantId = Guid.NewGuid();
        rooms.Factory.GetOrCreateContext(roomId).Connections.Register(Guid.NewGuid(), new ParticipantInfo { Id = participantId });

        // Act
        await service.KickParticipantAsync(roomId, participantId, "причина", CancellationToken.None);

        // Assert
        errorHandler.Verify(e => e.SendErrorAsync("причина", participantId, roomId, true, It.IsAny<int>()), Times.Once);
    }

    /// <summary>
    /// Запрос истории маршрутизируется с параметрами
    /// </summary>
    [Fact]
    public async Task SendChatHistoryRequestShouldRoute()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 1);
        var messageId = Guid.NewGuid();

        // Act
        await service.SendChatHistoryRequest(roomId, timestamp, messageId, CancellationToken.None);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<ChatHistoryRequestMessage>(m => m.OldestTimestamp == timestamp && m.OldestMessageId == messageId),
            roomId, identity.SelfParticipant.Id, It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Обновление информации о комнате хостом маршрутизируется
    /// </summary>
    [Fact]
    public async Task SendUpdatedRoomInfoWhenHostingShouldRoute()
    {
        // Arrange
        registry.RegisterServerConnection(roomId, Guid.NewGuid());
        var roomInfo = new RoomInfo { Id = roomId, Name = "Новое" };

        // Act
        await service.SendUpdatedRoomInfoAsync(roomInfo, [], CancellationToken.None);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<RoomInfoUpdatedMessage>(m => m.Room.Name == "Новое"),
            roomId, identity.SelfParticipant.Id, It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Обновление информации о комнате не хостом отклоняется
    /// </summary>
    [Fact]
    public async Task SendUpdatedRoomInfoWhenNotHostingShouldThrow()
    {
        // Act
        var act = () => service.SendUpdatedRoomInfoAsync(new RoomInfo { Id = roomId }, [], CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Включение обнаружения в настройках запускает discovery
    /// </summary>
    [Fact]
    public async Task EnableDiscoveryShouldStart()
    {
        // Arrange
        var roomInfo = new RoomInfo { Id = roomId };
        var oldOptions = new NetworkOptions { EnableLocalDiscovery = false };
        var newOptions = new NetworkOptions { EnableLocalDiscovery = true };

        // Act
        await service.ManageDiscoveryOutOfSettings(roomInfo, [], newOptions, oldOptions, CancellationToken.None);

        // Assert
        discovery.Verify(d => d.StartDiscoveryAsync(roomInfo, It.IsAny<IEnumerable<IEndpoint>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Выключение обнаружения в настройках останавливает discovery
    /// </summary>
    [Fact]
    public async Task DisableDiscoveryShouldStop()
    {
        // Arrange
        var roomInfo = new RoomInfo { Id = roomId };
        var oldOptions = new NetworkOptions { EnableLocalDiscovery = true };
        var newOptions = new NetworkOptions { EnableLocalDiscovery = false };

        // Act
        await service.ManageDiscoveryOutOfSettings(roomInfo, [], newOptions, oldOptions, CancellationToken.None);

        // Assert
        discovery.Verify(d => d.StopDiscoveryAsync(roomId), Times.Once);
    }

    /// <inheritdoc />
    public void Dispose() => rooms.Dispose();
}
