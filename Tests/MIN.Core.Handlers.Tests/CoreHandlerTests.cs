using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.RoomRelated;
using MIN.Core.Messaging.Stateless.RoomRelated.History;
using MIN.Core.Messaging.Stateless.RoomRelated.Messages;
using MIN.Core.Messaging.Stateless.RoomRelated.Ping;
using MIN.Core.Stores.Contracts.Models;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Handlers.Tests;

/// <summary>
/// Тесты обработчиков Core: история, ping, ошибки, участники, редактирование, удаление
/// </summary>
public class CoreHandlerTests : IDisposable
{
    private readonly HandlerTestFactory factory = new();
    private readonly RoomContext roomContext;
    private readonly Guid selfId = Guid.NewGuid();
    private readonly Guid connectionId = Guid.NewGuid();
    private readonly Guid roomId;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="CoreHandlerTests"/>
    /// </summary>
    public CoreHandlerTests()
    {
        roomId = Guid.NewGuid();
        roomContext = factory.RoomFactory.GetOrCreateContext(roomId);
    }

    private MessageContext CreateContext(Role role = Role.Client)
        => TestMessageContext.Create(roomContext, selfId, connectionId, role);

    private void RegisterRoom(Guid? hostId = null)
    {
        var room = new Room
        {
            Id = roomId,
            Name = "Комната",
            HostParticipant = new MIN.Core.Entities.Contracts.Models.ParticipantInfo { Id = hostId ?? selfId },
        };
        factory.RoomStore.Register(room);
    }

    /// <summary>
    /// Ping порождает Pong-ответ
    /// </summary>
    [Fact]
    public async Task PingShouldReturnPong()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.Ping);

        // Act
        var result = await handler.HandleAsync(new PingMessage { SenderId = selfId }, CreateContext());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Response.Should().BeOfType<PongMessage>();
    }

    /// <summary>
    /// Pong порождает событие измерения
    /// </summary>
    [Fact]
    public async Task PongShouldPublishEvent()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.Pong);

        // Act
        var result = await handler.HandleAsync(new PongMessage { SenderId = selfId }, CreateContext());

        // Assert
        result.ResultEvent.Should().BeOfType<PingPongReceivedEvent>();
    }

    /// <summary>
    /// Запрос истории возвращает видимые сообщения старше якоря
    /// </summary>
    [Fact]
    public async Task ChatHistoryRequestShouldReturnVisibleHistory()
    {
        // Arrange
        RegisterRoom();
        var requesterId = Guid.NewGuid();
        var anchorTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        roomContext.Messages.AddMessage(new ChatTextMessage { Content = "старое публичное", Timestamp = anchorTime.AddMinutes(-1), SenderId = selfId });
        roomContext.Messages.AddMessage(new ChatTextMessage { Content = "чужое личное", Timestamp = anchorTime.AddMinutes(-1), SenderId = selfId, RecipientId = Guid.NewGuid() });
        roomContext.Messages.AddMessage(new ChatTextMessage { Content = "новое", Timestamp = anchorTime.AddMinutes(1), SenderId = selfId });
        var handler = factory.GetHandler(MessageTypeTag.ChatHistoryRequest);
        var request = new ChatHistoryRequestMessage { SenderId = requesterId, OldestTimestamp = anchorTime, PageSize = 10 };

        // Act
        var result = await handler.HandleAsync(request, CreateContext(Role.Host));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StopPropagation.Should().BeTrue();
        var response = result.Response.Should().BeOfType<ChatHistoryResponseMessage>().Subject;
        response.Messages.Should().HaveCount(1);
        ((ChatTextMessage)response.Messages[0]).Content.Should().Be("старое публичное");
    }

    /// <summary>
    /// Ответ истории от хоста на клиенте добавляет сообщения в начало
    /// </summary>
    [Fact]
    public async Task ChatHistoryResponseFromHostShouldPrependMessages()
    {
        // Arrange
        var hostId = Guid.NewGuid();
        RegisterRoom(hostId);
        roomContext.Messages.AddMessage(new ChatTextMessage { Content = "уже есть" });
        var handler = factory.GetHandler(MessageTypeTag.ChatHistoryResponse);
        // Ответ истории приходит в порядке «новые первыми», prepend разворачивает в хронологию
        var response = new ChatHistoryResponseMessage
        {
            SenderId = hostId,
            Messages = [new ChatTextMessage { Content = "из истории новее" }, new ChatTextMessage { Content = "из истории старее" }],
        };

        // Act
        var result = await handler.HandleAsync(response, CreateContext(Role.Client));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var history = roomContext.Messages.GetHistory().Cast<ChatTextMessage>().ToList();
        history.Should().HaveCount(3);
        history[0].Content.Should().Be("из истории старее");
        history[1].Content.Should().Be("из истории новее");
        history[2].Content.Should().Be("уже есть");
    }

    /// <summary>
    /// Ответ истории не от хоста не добавляет сообщения
    /// </summary>
    [Fact]
    public async Task ChatHistoryResponseFromNonHostShouldNotAddMessages()
    {
        // Arrange
        RegisterRoom(hostId: Guid.NewGuid());
        var handler = factory.GetHandler(MessageTypeTag.ChatHistoryResponse);
        var response = new ChatHistoryResponseMessage
        {
            SenderId = Guid.NewGuid(),
            Messages = [new ChatTextMessage { Content = "поддельное" }],
        };

        // Act
        await handler.HandleAsync(response, CreateContext(Role.Client));

        // Assert
        roomContext.Messages.GetMessageCount().Should().Be(0);
    }

    /// <summary>
    /// Очистка истории удаляет сообщения и оставляет системную запись
    /// </summary>
    [Fact]
    public async Task ChatHistoryClearShouldClearAndAddSystemMessage()
    {
        // Arrange
        RegisterRoom();
        using var deletedCapture = new EventCapture<MessageDeletedEvent>(factory.EventBus);
        roomContext.Messages.AddMessage(new ChatTextMessage { Content = "первое" });
        roomContext.Messages.AddMessage(new ChatTextMessage { Content = "второе" });
        var handler = factory.GetHandler(MessageTypeTag.ChatHistoryClear);
        var clear = new ChatHistoryClearMessage { SenderId = selfId };

        // Act
        var result = await handler.HandleAsync(clear, CreateContext());

        // Assert
        result.IsSuccess.Should().BeTrue();
        await deletedCapture.WaitAsync(2);
        result.ResultEvent.Should().BeOfType<MIN.Core.Events.Events.ChatHistoryClearedEvent>();
        roomContext.Messages.GetMessageCount().Should().Be(1);
        roomContext.Messages.GetFirstMessage().Should().BeOfType<SystemTextMessage>();
    }

    /// <summary>
    /// Редактирование своего сообщения на хосте обновляет контент и ответы на него
    /// </summary>
    [Fact]
    public async Task MessageUpdateShouldEditContentAndReplyDescriptions()
    {
        // Arrange
        var original = new ChatTextMessage { Content = "старый текст", SenderId = selfId, SenderName = "Я" };
        roomContext.Messages.AddMessage(original);
        var reply = new ChatTextMessage { Content = "ответ", SenderId = selfId, ReplyToMessageId = original.Id, ReplyToMessageDescription = "старый текст" };
        roomContext.Messages.AddMessage(reply);
        var handler = factory.GetHandler(MessageTypeTag.MessageUpdate);
        var update = new MessageUpdateMessage
        {
            SenderId = selfId,
            MessageIdToEdit = original.Id,
            NewMessage = new ChatTextMessage { Content = "новый текст", SenderName = "Я" },
        };

        // Act
        var result = await handler.HandleAsync(update, CreateContext(Role.Host));

        // Assert
        result.IsSuccess.Should().BeTrue();
        original.Content.Should().Be("новый текст");
        original.IsUpdated.Should().BeTrue();
        reply.ReplyToMessageDescription.Should().Be("Я: новый текст");
        result.ResultEvent.Should().BeOfType<MessageUpdatedEvent>();
    }

    /// <summary>
    /// Редактирование чужого сообщения на хосте отклоняется
    /// </summary>
    [Fact]
    public async Task MessageUpdateForeignShouldBeRejectedOnHost()
    {
        // Arrange
        var original = new ChatTextMessage { Content = "чужое", SenderId = Guid.NewGuid() };
        roomContext.Messages.AddMessage(original);
        var handler = factory.GetHandler(MessageTypeTag.MessageUpdate);
        var update = new MessageUpdateMessage
        {
            SenderId = selfId,
            MessageIdToEdit = original.Id,
            NewMessage = new ChatTextMessage { Content = "взлом" },
        };

        // Act
        var result = await handler.HandleAsync(update, CreateContext(Role.Host));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().NotBeNull();
        original.Content.Should().Be("чужое");
    }

    /// <summary>
    /// Редактирование несуществующего сообщения на хосте даёт обработанную ошибку
    /// </summary>
    [Fact]
    public async Task MessageUpdateMissingShouldBeHandledOnHost()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.MessageUpdate);
        var update = new MessageUpdateMessage
        {
            SenderId = selfId,
            MessageIdToEdit = Guid.NewGuid(),
            NewMessage = new ChatTextMessage { Content = "несуществующее" },
        };

        // Act
        var result = await handler.HandleAsync(update, CreateContext(Role.Host));

        // Assert
        result.ErrorMessage.Should().NotBeNull();
    }

    /// <summary>
    /// Удаление своего сообщения удаляет его и чистит ответы
    /// </summary>
    [Fact]
    public async Task MessageDeleteShouldRemoveAndCleanReplies()
    {
        // Arrange
        var original = new ChatTextMessage { Content = "удаляемое", SenderId = selfId };
        roomContext.Messages.AddMessage(original);
        var reply = new ChatTextMessage { Content = "ответ", SenderId = selfId, ReplyToMessageId = original.Id, ReplyToMessageDescription = "удаляемое" };
        roomContext.Messages.AddMessage(reply);
        var handler = factory.GetHandler(MessageTypeTag.MessageDelete);
        var delete = new MessageDeleteMessage { SenderId = selfId, MessageIdToDelete = original.Id };

        // Act
        var result = await handler.HandleAsync(delete, CreateContext(Role.Host));

        // Assert
        result.IsSuccess.Should().BeTrue();
        roomContext.Messages.GetMessageById(original.Id).Should().BeNull();
        reply.ReplyToMessageId.Should().BeNull();
        reply.ReplyToMessageDescription.Should().Be("Сообщение было удалено");
        result.ResultEvent.Should().BeOfType<MessageDeletedEvent>();
    }

    /// <summary>
    /// Удаление чужого сообщения не отправителем и не хостом отклоняется
    /// </summary>
    [Fact]
    public async Task MessageDeleteForeignShouldBeRejected()
    {
        // Arrange
        var original = new ChatTextMessage { Content = "чужое", SenderId = Guid.NewGuid() };
        roomContext.Messages.AddMessage(original);
        var handler = factory.GetHandler(MessageTypeTag.MessageDelete);
        var delete = new MessageDeleteMessage { SenderId = Guid.NewGuid(), MessageIdToDelete = original.Id };

        // Act
        var result = await handler.HandleAsync(delete, CreateContext(Role.Host));

        // Assert
        result.ErrorMessage.Should().NotBeNull();
        roomContext.Messages.GetMessageById(original.Id).Should().NotBeNull();
    }

    /// <summary>
    /// Хост может удалить чужое сообщение
    /// </summary>
    [Fact]
    public async Task HostCanDeleteForeignMessage()
    {
        // Arrange
        var original = new ChatTextMessage { Content = "чужое", SenderId = Guid.NewGuid() };
        roomContext.Messages.AddMessage(original);
        var handler = factory.GetHandler(MessageTypeTag.MessageDelete);
        var delete = new MessageDeleteMessage { SenderId = selfId, MessageIdToDelete = original.Id };

        // Act
        var result = await handler.HandleAsync(delete, CreateContext(Role.Host));

        // Assert
        result.IsSuccess.Should().BeTrue();
        roomContext.Messages.GetMessageById(original.Id).Should().BeNull();
    }

    /// <summary>
    /// Удаление неудаляемого типа сообщения отклоняется
    /// </summary>
    [Fact]
    public async Task MessageDeleteNonDeletableShouldBeRejected()
    {
        // Arrange
        var system = new SystemTextMessage { Content = "системное", SenderId = selfId };
        roomContext.Messages.AddMessage(system);
        var handler = factory.GetHandler(MessageTypeTag.MessageDelete);
        var delete = new MessageDeleteMessage { SenderId = selfId, MessageIdToDelete = system.Id };

        // Act
        var result = await handler.HandleAsync(delete, CreateContext(Role.Host));

        // Assert
        result.ErrorMessage.Should().NotBeNull();
        roomContext.Messages.GetMessageById(system.Id).Should().NotBeNull();
    }

    /// <summary>
    /// ParticipantLeft с IsLeftRoom удаляет участника
    /// </summary>
    [Fact]
    public async Task ParticipantLeftShouldRemoveParticipant()
    {
        // Arrange
        var leaving = new Participant { Id = Guid.NewGuid(), Name = "Уходящий" };
        roomContext.Participants.AddParticipant(leaving);
        var handler = factory.GetHandler(MessageTypeTag.ParticipantLeft);
        var message = new MIN.Core.Messaging.RoomRelated.ParticipantRelated.ParticipantLeftMessage
        {
            Participant = leaving.ToParticipantInfo(),
            Reason = DisconnectReason.LeftRoom,
        };

        // Act
        var result = await handler.HandleAsync(message, CreateContext());

        // Assert
        result.IsSuccess.Should().BeTrue();
        roomContext.Participants.TryGetParticipantById(leaving.Id, out _).Should().BeFalse();
        result.ResultEvent.Should().BeOfType<ParticipantLeftEvent>();
    }

    /// <summary>
    /// ParticipantLeft без выхода из комнаты переводит в оффлайн
    /// </summary>
    [Fact]
    public async Task ParticipantLeftWithoutLeaveShouldMarkOffline()
    {
        // Arrange
        var leaving = new Participant { Id = Guid.NewGuid(), Name = "Отключившийся", CurrentStatus = OnlineStatus.Online };
        roomContext.Participants.AddParticipant(leaving);
        var handler = factory.GetHandler(MessageTypeTag.ParticipantLeft);
        var message = new MIN.Core.Messaging.RoomRelated.ParticipantRelated.ParticipantLeftMessage
        {
            Participant = leaving.ToParticipantInfo(),
            Reason = DisconnectReason.Timeout,
        };

        // Act
        await handler.HandleAsync(message, CreateContext());

        // Assert
        leaving.CurrentStatus.Should().Be(OnlineStatus.Offline);
        leaving.LastSeenOnline.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        roomContext.Participants.TryGetParticipantById(leaving.Id, out _).Should().BeTrue();
    }

    /// <summary>
    /// ParticipantUpdated обновляет имя участника
    /// </summary>
    [Fact]
    public async Task ParticipantUpdatedShouldUpdateName()
    {
        // Arrange
        var participant = new Participant { Id = Guid.NewGuid(), Name = "Старое" };
        roomContext.Participants.AddParticipant(participant);
        var handler = factory.GetHandler(MessageTypeTag.ParticipantUpdated);
        var message = new MIN.Core.Messaging.Stateless.RoomRelated.ParticipantRelated.ParticipantUpdatedMessage
        {
            SenderId = participant.Id,
            NewParticipant = new Participant { Id = participant.Id, Name = "Новое" },
        };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Client));

        // Assert
        result.IsSuccess.Should().BeTrue();
        roomContext.Participants.GetParticipantById(participant.Id).Name.Should().Be("Новое");
        result.ResultEvent.Should().BeOfType<ParticipantUpdatedEvent>();
    }

    /// <summary>
    /// Хост игнорирует обновление чужого участника не от его имени
    /// </summary>
    [Fact]
    public async Task ParticipantUpdatedForeignShouldBeIgnoredOnHost()
    {
        // Arrange
        var participant = new Participant { Id = Guid.NewGuid(), Name = "Жертва" };
        roomContext.Participants.AddParticipant(participant);
        var handler = factory.GetHandler(MessageTypeTag.ParticipantUpdated);
        var message = new MIN.Core.Messaging.Stateless.RoomRelated.ParticipantRelated.ParticipantUpdatedMessage
        {
            SenderId = Guid.NewGuid(),
            NewParticipant = new Participant { Id = participant.Id, Name = "Взлом" },
        };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Host));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StopPropagation.Should().BeTrue();
        roomContext.Participants.GetParticipantById(participant.Id).Name.Should().Be("Жертва");
    }

    /// <summary>
    /// Ошибка от хоста проваливает обработку
    /// </summary>
    [Fact]
    public async Task ErrorMessageShouldFail()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.Error);
        var message = new MIN.Core.Messaging.Stateless.ErrorMessage { Message = "что-то сломалось" };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Client));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("что-то сломалось");
    }

    /// <inheritdoc />
    public void Dispose() => factory.Dispose();
}
