using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.RoomRelated;
using MIN.Core.Messaging.Stateless.RoomRelated.History;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class ChatHistoryHandler : BaseHandler
{
    private readonly IEventBus eventBus;
    private readonly IRoomStore roomStore;

    public ChatHistoryHandler(IEventBus eventBus,
        IRoomStore roomStore,
        ILoggerProvider logger) : base(logger)
    {
        this.eventBus = eventBus;
        this.roomStore = roomStore;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.ChatHistoryRequest, MessageTypeTag.ChatHistoryResponse, MessageTypeTag.ChatHistoryClear];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var roomId = context.RoomContext.RoomId;

        switch (message)
        {
            case ChatHistoryRequestMessage request:
                var totalCount = roomStore.GetRoomChatHistoryCountFor(request.SenderId, roomId);
                var olderMessages = context.RoomContext
                    .Messages
                    .GetMessagesOlderThan(request.OldestTimestamp, request.OldestMessageId, request.PageSize)
                    .ToList();

                return HandlerResult.WithResponse(new ChatHistoryResponseMessage
                {
                    Messages = olderMessages,
                    TotalCount = totalCount,
                    OldestTimestamp = request.OldestTimestamp,
                    OldestMessageId = request.OldestMessageId,
                }, stopPropagation: true);

            case ChatHistoryResponseMessage response:
                if (roomStore.GetRoomHostParticipantId(roomId) == response.SenderId
                    && context.Role == Role.Client)
                {
                    foreach (var roomMessage in response.Messages)
                    {
                        context.RoomContext.Messages.AddMessage(roomMessage, appendOnStart: true);
                    }
                }

                return HandlerResult.WithEvent(new ChatHistoryUpdatedEvent()
                {
                    RoomId = roomId,
                    Message = response,
                });

            case ChatHistoryClearMessage clearMessage:
                var room = roomStore.GetRoom(roomId);
                room.LocalRoomSettings.HistoryWipedOutUpTo = DateTime.Now;

                var allMessages = context.RoomContext.Messages.GetHistory();

                foreach (var messageToClear in allMessages)
                {
                    await eventBus.PublishAsync(new MessageDeletedEvent()
                    {
                        MessageId = messageToClear.Id,
                        RoomId = roomId,
                    }, context.CancellationToken);
                }

                context.RoomContext.Messages.ClearMessages();
                context.RoomContext.Messages.AddMessage(new SystemTextMessage()
                {
                    Content = (clearMessage as IDescribable).GetDescription()
                });
                room.TotalMessageCount = context.RoomContext.Messages.GetMessageCount();

                return HandlerResult.WithEvent(new ChatHistoryClearedEvent()
                {
                    RoomId = roomId,
                    Message = clearMessage,
                });

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
