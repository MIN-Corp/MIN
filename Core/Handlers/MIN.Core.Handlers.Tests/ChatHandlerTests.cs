using FluentAssertions;
using MIN.Chat.Events;
using MIN.Chat.Messaging;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Stores.Contracts.Models;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Handlers.Tests;

/// <summary>
/// Тесты обработчиков Chat: ChatTextHandler и OnlineStatusHandler
/// </summary>
public class ChatHandlerTests : IDisposable
{
    private readonly HandlerTestFactory factory = new();
    private readonly RoomContext roomContext;
    private readonly Guid selfId = Guid.NewGuid();
    private readonly Guid connectionId = Guid.NewGuid();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatHandlerTests"/>
    /// </summary>
    public ChatHandlerTests()
    {
        roomContext = factory.RoomFactory.GetOrCreateContext(Guid.NewGuid());
    }

    private MessageContext CreateContext(Role role = Role.Client)
        => TestMessageContext.Create(roomContext, selfId, connectionId, role);

    /// <summary>
    /// Публичное текстовое сообщение добавляется в историю и порождает событие
    /// </summary>
    [Fact]
    public async Task PublicChatTextShouldBeStoredAndNotified()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        roomContext.Participants.AddParticipant(new Participant { Id = senderId, Name = "Отправитель" });
        var handler = factory.GetHandler(MessageTypeTag.ChatTextMessage);
        var message = new ChatTextMessage { Content = "привет", SenderId = senderId, SenderName = "Отправитель" };

        // Act
        var result = await handler.HandleAsync(message, CreateContext());

        // Assert
        result.IsSuccess.Should().BeTrue();
        roomContext.Messages.GetMessageById(message.Id).Should().BeSameAs(message);
        var receivedEvent = result.ResultEvent.Should().BeOfType<ChatTextMessageReceivedEvent>().Subject;
        receivedEvent.Message.Should().BeSameAs(message);
        receivedEvent.Sender.Id.Should().Be(senderId);
    }

    /// <summary>
    /// Сообщение от неизвестного отправителя отклоняется
    /// </summary>
    [Fact]
    public async Task ChatTextFromUnknownSenderShouldFail()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.ChatTextMessage);
        var message = new ChatTextMessage { Content = "привет", SenderId = Guid.NewGuid() };

        // Act
        var result = await handler.HandleAsync(message, CreateContext());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.CriticalErrorForConnection.Should().BeTrue();
        roomContext.Messages.GetMessageCount().Should().Be(0);
    }

    /// <summary>
    /// Чужое приватное сообщение сохраняется, но событие не публикуется
    /// </summary>
    [Fact]
    public async Task ForeignPrivateChatTextShouldBeStoredWithoutEvent()
    {
        // Arrange
        using var capture = new EventCapture<ChatTextMessageReceivedEvent>(factory.EventBus);
        var senderId = Guid.NewGuid();
        roomContext.Participants.AddParticipant(new Participant { Id = senderId });
        var handler = factory.GetHandler(MessageTypeTag.ChatTextMessage);
        var message = new ChatTextMessage { Content = "личное", SenderId = senderId, RecipientId = Guid.NewGuid() };

        // Act
        var result = await handler.HandleAsync(message, CreateContext());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResultEvent.Should().BeNull();
        roomContext.Messages.GetMessageById(message.Id).Should().NotBeNull();
        capture.Events.Should().BeEmpty();
    }

    /// <summary>
    /// Приватное сообщение мне порождает событие
    /// </summary>
    [Fact]
    public async Task PrivateChatTextToMeShouldNotify()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        roomContext.Participants.AddParticipant(new Participant { Id = senderId });
        var handler = factory.GetHandler(MessageTypeTag.ChatTextMessage);
        var message = new ChatTextMessage { Content = "лично мне", SenderId = senderId, RecipientId = selfId };

        // Act
        var result = await handler.HandleAsync(message, CreateContext());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResultEvent.Should().BeOfType<ChatTextMessageReceivedEvent>();
    }

    /// <summary>
    /// Статус известного участника обновляется и публикуется
    /// </summary>
    [Fact]
    public async Task OnlineStatusShouldUpdateParticipant()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var participant = new Participant { Id = senderId, CurrentStatus = OnlineStatus.Online };
        roomContext.Participants.AddParticipant(participant);
        var handler = factory.GetHandler(MessageTypeTag.OnlineStatusChanged);
        var message = new OnlineStatusChangedMessage { SenderId = senderId, Status = OnlineStatus.Offline };

        // Act
        var result = await handler.HandleAsync(message, CreateContext());

        // Assert
        result.IsSuccess.Should().BeTrue();
        participant.CurrentStatus.Should().Be(OnlineStatus.Offline);
        participant.LastSeenOnline.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        var statusEvent = result.ResultEvent.Should().BeOfType<OnlineStatusChangedEvent>().Subject;
        statusEvent.Participant.Id.Should().Be(senderId);
    }

    /// <summary>
    /// Собственный статус не порождает событие
    /// </summary>
    [Fact]
    public async Task OwnOnlineStatusShouldBeIgnored()
    {
        // Arrange
        using var capture = new EventCapture<OnlineStatusChangedEvent>(factory.EventBus);
        var handler = factory.GetHandler(MessageTypeTag.OnlineStatusChanged);
        var message = new OnlineStatusChangedMessage { SenderId = selfId, Status = OnlineStatus.Offline };

        // Act
        var result = await handler.HandleAsync(message, CreateContext());

        // Assert
        result.IsSuccess.Should().BeTrue();
        capture.Events.Should().BeEmpty();
    }

    /// <summary>
    /// Статус от неизвестного участника обрабатывается без падения
    /// </summary>
    [Fact]
    public async Task OnlineStatusFromUnknownSenderShouldNotThrow()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.OnlineStatusChanged);
        var message = new OnlineStatusChangedMessage { SenderId = Guid.NewGuid(), Status = OnlineStatus.Online };

        // Act
        var result = await handler.HandleAsync(message, CreateContext());

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Обработчик отклоняет сообщение чужого типа
    /// </summary>
    [Fact]
    public async Task WrongMessageTypeShouldFail()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.ChatTextMessage);

        // Act
        var result = await handler.HandleAsync(new Messaging.Stateless.RoomRelated.Ping.PingMessage(), CreateContext());

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    /// <inheritdoc />
    public void Dispose() => factory.Dispose();
}
