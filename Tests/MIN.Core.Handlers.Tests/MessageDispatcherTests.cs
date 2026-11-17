using FluentAssertions;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Handlers.Dispatcher;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Chat.Messaging;
using MIN.Core.Messaging.Stateless.RoomRelated.Ping;
using MIN.Core.Stores.Contracts.Models;
using MIN.Core.Stores.Contracts.Registries.Models;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Core.Handlers.Tests;

/// <summary>
/// Тесты на <see cref="MessageDispatcher"/>
/// </summary>
public class MessageDispatcherTests : IDisposable
{
    private readonly HandlerTestFactory factory = new();
    private readonly RoomContext roomContext;
    private readonly Guid selfId = Guid.NewGuid();
    private readonly Guid connectionId = Guid.NewGuid();

    /// <summary>
    /// Тестовый обработчик с настраиваемым результатом
    /// </summary>
    private sealed class FakeHandler : IMessageHandler
    {
        private readonly Func<IMessage, MessageContext, Task<HandlerResult>> handle;

        public IEnumerable<MessageTypeTag> HandledTypes { get; }
        public int Priority { get; }
        public int Calls;

        public FakeHandler(MessageTypeTag tag, Func<IMessage, MessageContext, Task<HandlerResult>> handle, int priority = 0)
        {
            HandledTypes = [tag];
            this.handle = handle;
            Priority = priority;
        }

        public Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
        {
            Interlocked.Increment(ref Calls);
            return handle(message, context);
        }
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MessageDispatcherTests"/>
    /// </summary>
    public MessageDispatcherTests()
    {
        roomContext = factory.RoomFactory.GetOrCreateContext(Guid.NewGuid());
    }

    private MessageDispatcher CreateDispatcher(params IMessageHandler[] handlers)
        => new(handlers, factory.MessageSender.Object, factory.EventBus, factory.Persistence.Object, factory.NetworkErrorHandler.Object, factory.Logger);

    private MessageContext CreateContext(Role role = Role.Client)
        => TestMessageContext.Create(roomContext, selfId, connectionId, role);

    /// <summary>
    /// Без обработчика диспетчер бросает NotImplementedException
    /// </summary>
    [Fact]
    public async Task NoHandlerShouldThrowNotImplemented()
    {
        // Arrange
        var dispatcher = CreateDispatcher();

        // Act
        var act = () => dispatcher.DispatchAsync(new PingMessage(), CreateContext(), null);

        // Assert
        await act.Should().ThrowAsync<NotImplementedException>();
    }

    /// <summary>
    /// Обработчики вызываются в порядке приоритета
    /// </summary>
    [Fact]
    public async Task HandlersShouldRunByPriority()
    {
        // Arrange
        var order = new List<string>();
        var late = new FakeHandler(MessageTypeTag.Ping, (_, _) => { order.Add("late"); return Task.FromResult(HandlerResult.Success()); }, priority: 10);
        var early = new FakeHandler(MessageTypeTag.Ping, (_, _) => { order.Add("early"); return Task.FromResult(HandlerResult.Success()); }, priority: 1);
        var dispatcher = CreateDispatcher(late, early);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(), null);

        // Assert
        order.Should().Equal("early", "late");
    }

    /// <summary>
    /// StopPropagation у успешного результата останавливает цепочку
    /// </summary>
    [Fact]
    public async Task SuccessStopPropagationShouldStopChain()
    {
        // Arrange
        var first = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Success(stopPropagation: true)));
        var second = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Success()));
        var dispatcher = CreateDispatcher(first, second);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(), null);

        // Assert
        first.Calls.Should().Be(1);
        second.Calls.Should().Be(0);
    }

    /// <summary>
    /// StopPropagation у провального результата останавливает цепочку
    /// </summary>
    [Fact]
    public async Task FailureStopPropagationShouldStopChain()
    {
        // Arrange
        var first = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Failure("ошибка", stopPropagation: true)));
        var second = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Success()));
        var dispatcher = CreateDispatcher(first, second);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(), null);

        // Assert
        first.Calls.Should().Be(1);
        second.Calls.Should().Be(0);
    }

    /// <summary>
    /// StopPropagation у результата с обработанной ошибкой останавливает цепочку
    /// </summary>
    [Fact]
    public async Task ErrorHandledStopPropagationShouldStopChain()
    {
        // Arrange
        var first = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.WithErrorHandled("ошибка", stopPropagation: true)));
        var second = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Success()));
        var dispatcher = CreateDispatcher(first, second);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(), null);

        // Assert
        first.Calls.Should().Be(1);
        second.Calls.Should().Be(0);
    }

    /// <summary>
    /// Исключение в обработчике не прерывает цепочку и публикует событие ошибки
    /// </summary>
    [Fact]
    public async Task HandlerExceptionShouldNotBreakChain()
    {
        // Arrange
        using var errorCapture = new EventCapture<ErrorOccurredEvent>(factory.EventBus);
        var first = new FakeHandler(MessageTypeTag.Ping, (_, _) => throw new InvalidOperationException("бум"));
        var second = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Success()));
        var dispatcher = CreateDispatcher(first, second);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(), null);

        // Assert
        second.Calls.Should().Be(1);
        await errorCapture.WaitAsync();
        errorCapture.Events.Single().ErrorMessage.Should().Contain("бум");
    }

    /// <summary>
    /// Провал с ShowErrorMessage публикует событие ошибки
    /// </summary>
    [Fact]
    public async Task FailureShouldPublishErrorEvent()
    {
        // Arrange
        using var errorCapture = new EventCapture<ErrorOccurredEvent>(factory.EventBus);
        var handler = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Failure("плохо")));
        var dispatcher = CreateDispatcher(handler);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(Role.Client), null);

        // Assert
        await errorCapture.WaitAsync();
        errorCapture.Events.Single().ErrorMessage.Should().Be("плохо");
    }

    /// <summary>
    /// Критический провал на клиенте требует отключения
    /// </summary>
    [Fact]
    public async Task CriticalClientFailureShouldRequestDisconnect()
    {
        // Arrange
        using var errorCapture = new EventCapture<ErrorOccurredEvent>(factory.EventBus);
        var handler = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Failure("критично", critical: true)));
        var dispatcher = CreateDispatcher(handler);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(Role.Client), null);

        // Assert
        await errorCapture.WaitAsync();
        errorCapture.Events.Single().NeedToDisconnect.Should().BeTrue();
    }

    /// <summary>
    /// Response обработчика отправляется в соединение
    /// </summary>
    [Fact]
    public async Task ResponseShouldBeSentToConnection()
    {
        // Arrange
        var response = new PongMessage();
        var handler = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.WithResponse(response)));
        var dispatcher = CreateDispatcher(handler);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(), null);

        // Assert
        factory.MessageSender.Verify(s => s.SendAsync(response, roomContext.RoomId, connectionId, It.IsAny<CancellationToken>()), Times.Once);
        response.SenderId.Should().Be(selfId);
    }

    /// <summary>
    /// Response для локального соединения диспетчеризуется рекурсивно
    /// </summary>
    [Fact]
    public async Task LocalResponseShouldBeDispatchedRecursively()
    {
        // Arrange
        var pongHandler = new FakeHandler(MessageTypeTag.Pong, (_, _) => Task.FromResult(HandlerResult.Success()));
        var pingHandler = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.WithResponse(new PongMessage())));
        var dispatcher = CreateDispatcher(pingHandler, pongHandler);
        var localContext = TestMessageContext.Create(roomContext, selfId, CoreRegistryConstants.LocalConnectionId, Role.Host);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, localContext, null);

        // Assert
        pongHandler.Calls.Should().Be(1);
        factory.MessageSender.Verify(s => s.SendAsync(It.IsAny<IMessage>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// ResultEvent обработчика публикуется в шину
    /// </summary>
    [Fact]
    public async Task ResultEventShouldBePublished()
    {
        // Arrange
        using var capture = new EventCapture<PingPongReceivedEvent>(factory.EventBus);
        var pingPongEvent = new PingPongReceivedEvent { RoomId = roomContext.RoomId };
        var handler = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.WithEvent(pingPongEvent)));
        var dispatcher = CreateDispatcher(handler);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(), null);

        // Assert
        await capture.WaitAsync();
        capture.Events.Single().Should().BeSameAs(pingPongEvent);
    }

    /// <summary>
    /// Хост рассылает публичное сообщение участникам, исключая отправителя
    /// </summary>
    [Fact]
    public async Task HostPublicMessageShouldBroadcast()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var senderConnection = Guid.NewGuid();
        roomContext.Participants.AddParticipant(new MIN.Core.Entities.Participant { Id = senderId });
        roomContext.Connections.Register(senderConnection, new MIN.Core.Entities.Contracts.Models.ParticipantInfo { Id = senderId });
        var handler = new FakeHandler(MessageTypeTag.ChatTextMessage, (_, _) => Task.FromResult(HandlerResult.Success()));
        var dispatcher = CreateDispatcher(handler);
        var message = new ChatTextMessage { SenderId = senderId, Content = "всем" };

        // Act
        await dispatcher.DispatchAsync(message, CreateContext(Role.Host), null);

        // Assert
        factory.MessageSender.Verify(s => s.BroadcastAsync(message, roomContext.RoomId,
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(senderConnection)), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Хост маршрутизирует приватное сообщение получателю
    /// </summary>
    [Fact]
    public async Task HostPrivateMessageShouldRouteToRecipient()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var recipientConnection = Guid.NewGuid();
        roomContext.Participants.AddParticipant(new MIN.Core.Entities.Participant { Id = recipientId });
        roomContext.Connections.Register(recipientConnection, new MIN.Core.Entities.Contracts.Models.ParticipantInfo { Id = recipientId });
        var handler = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Success()));
        var dispatcher = CreateDispatcher(handler);
        var message = new PingMessage { SenderId = selfId, RecipientId = recipientId };

        // Act
        await dispatcher.DispatchAsync(message, CreateContext(Role.Host), null);

        // Assert
        factory.MessageSender.Verify(s => s.SendAsync(message, roomContext.RoomId, recipientConnection, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Два успешных обработчика не должны приводить к дублирующей рассылке
    /// </summary>
    [Fact]
    public async Task MultipleSuccessHandlersShouldNotDuplicateBroadcast()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        roomContext.Participants.AddParticipant(new MIN.Core.Entities.Participant { Id = senderId });
        var first = new FakeHandler(MessageTypeTag.ChatTextMessage, (_, _) => Task.FromResult(HandlerResult.Success()));
        var second = new FakeHandler(MessageTypeTag.ChatTextMessage, (_, _) => Task.FromResult(HandlerResult.Success()));
        var dispatcher = CreateDispatcher(first, second);
        var message = new ChatTextMessage { SenderId = senderId, Content = "всем" };

        // Act
        await dispatcher.DispatchAsync(message, CreateContext(Role.Host), null);

        // Assert
        factory.MessageSender.Verify(s => s.BroadcastAsync(message, roomContext.RoomId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Диспетчер помечает комнату грязной для персистентности
    /// </summary>
    [Fact]
    public async Task DispatchShouldMarkRoomDirty()
    {
        // Arrange
        var handler = new FakeHandler(MessageTypeTag.Ping, (_, _) => Task.FromResult(HandlerResult.Success()));
        var dispatcher = CreateDispatcher(handler);

        // Act
        await dispatcher.DispatchAsync(new PingMessage { SenderId = selfId }, CreateContext(), null);

        // Assert
        factory.Persistence.Verify(p => p.MarkDirty(roomContext.RoomId), Times.Once);
    }

    /// <inheritdoc />
    public void Dispose() => factory.Dispose();
}
