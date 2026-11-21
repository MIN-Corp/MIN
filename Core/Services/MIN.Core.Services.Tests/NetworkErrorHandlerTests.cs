using MIN.Core.Events.Events;
using MIN.Core.Events.Services;
using MIN.Core.Messaging.Stateless;
using MIN.Core.Messaging.Stateless.RoomRelated.Disconnect;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Core.Services.Moderation;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Tests.Common;
using Moq;
using MIN.Core.Events.Contracts.Interfaces;
using Xunit;

namespace MIN.Core.Services.Tests;

/// <summary>
/// Тесты на <see cref="NetworkErrorHandler"/>
/// </summary>
public class NetworkErrorHandlerTests : IDisposable
{
    private readonly TestLoggerProvider logger = new();
    private readonly IEventBus eventBus;
    private readonly Mock<IRoomLifecycleManager> lifecycle = new();
    private readonly Mock<IMessageRouter> router = new();
    private readonly Mock<IMessageSender> sender = new();
    private readonly TestIdentityService identity = new();
    private readonly INetworkErrorHandler handler;
    private readonly Guid roomId = Guid.NewGuid();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="NetworkErrorHandlerTests"/>
    /// </summary>
    public NetworkErrorHandlerTests()
    {
        eventBus = new InMemoryEventBus(logger);
        handler = new NetworkErrorHandler(lifecycle.Object, router.Object, sender.Object, eventBus, identity);
    }

    /// <summary>
    /// Некритичная ошибка участнику маршрутизируется как ErrorMessage
    /// </summary>
    [Fact]
    public async Task NonCriticalErrorShouldRouteErrorMessage()
    {
        // Arrange
        var recipientId = Guid.NewGuid();

        // Act
        await handler.SendErrorAsync("некритично", recipientId, roomId);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<ErrorMessage>(m => m.Message == "некритично" && m.RecipientId == recipientId),
            roomId, identity.SelfParticipant.Id, It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Критичная ошибка участнику маршрутизируется как Disconnect
    /// </summary>
    [Fact]
    public async Task CriticalErrorShouldRouteDisconnect()
    {
        // Arrange
        var recipientId = Guid.NewGuid();

        // Act
        await handler.SendErrorAsync("критично", recipientId, roomId, critical: true, timeoutMs: 60_000);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<DisconnectMessage>(m => m.Reason == "критично" && m.RecipientId == recipientId),
            roomId, identity.SelfParticipant.Id, It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Некритичная ошибка соединению отправляется как ErrorMessage
    /// </summary>
    [Fact]
    public async Task NonCriticalConnectionErrorShouldSendErrorMessage()
    {
        // Arrange
        var connectionId = Guid.NewGuid();

        // Act
        await handler.SendErrorToConnectionAsync("некритично", connectionId, roomId);

        // Assert
        sender.Verify(s => s.SendAsync(
            It.Is<ErrorMessage>(m => m.Message == "некритично"),
            roomId, connectionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Критичная ошибка соединению отправляется как Disconnect
    /// </summary>
    [Fact]
    public async Task CriticalConnectionErrorShouldSendDisconnect()
    {
        // Arrange
        var connectionId = Guid.NewGuid();

        // Act
        await handler.SendErrorToConnectionAsync("критично", connectionId, roomId, critical: true, timeoutMs: 60_000);

        // Assert
        sender.Verify(s => s.SendAsync(
            It.Is<DisconnectMessage>(m => m.Reason == "критично"),
            roomId, connectionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// ACK на критичную ошибку участника приводит к кику участника
    /// </summary>
    [Fact]
    public async Task ParticipantDisconnectAckShouldKickClient()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        await handler.SendErrorAsync("причина", participantId, roomId, critical: true, timeoutMs: 60_000);

        // Act
        await eventBus.PublishAsync(new DisconnectAckReceived
        {
            RoomId = roomId,
            ParticipantId = participantId,
            ConnectionId = Guid.NewGuid(),
            ErrorMessage = "причина",
        }, CancellationToken.None);
        await TestWait.UntilAsync(() => lifecycle.Invocations.Count >= 1);

        // Assert
        lifecycle.Verify(l => l.KickClientAsync(roomId, participantId, DisconnectReason.Kick, "причина"), Times.Once);
    }

    /// <summary>
    /// ACK на критичную ошибку соединения приводит к кику соединения
    /// </summary>
    [Fact]
    public async Task ConnectionDisconnectAckShouldKickConnection()
    {
        // Arrange
        var connectionId = Guid.NewGuid();
        await handler.SendErrorToConnectionAsync("причина", connectionId, roomId, critical: true, timeoutMs: 60_000);

        // Act
        await eventBus.PublishAsync(new DisconnectAckReceived
        {
            RoomId = roomId,
            ParticipantId = Guid.NewGuid(),
            ConnectionId = connectionId,
        }, CancellationToken.None);
        await TestWait.UntilAsync(() => lifecycle.Invocations.Count >= 1, timeoutMs: 2000);

        // Assert
        lifecycle.Verify(l => l.KickConnectionAsync(roomId, connectionId, DisconnectReason.Kick), Times.Once);
    }

    /// <summary>
    /// Таймаут ожидания ACK по соединению приводит к кику соединения
    /// </summary>
    [Fact]
    public async Task ConnectionRejectTimeoutShouldKickConnection()
    {
        // Arrange
        var connectionId = Guid.NewGuid();

        // Act
        await handler.SendErrorToConnectionAsync("причина", connectionId, roomId, critical: true, timeoutMs: 100);
        await TestWait.UntilAsync(() => lifecycle.Invocations.Count >= 1, timeoutMs: 2000);

        // Assert
        lifecycle.Verify(l => l.KickConnectionAsync(roomId, connectionId, DisconnectReason.Kick), Times.Once);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
