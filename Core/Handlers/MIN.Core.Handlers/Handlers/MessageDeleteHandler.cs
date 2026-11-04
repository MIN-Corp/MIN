using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.Messages;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class MessageDeleteHandler : BaseHandler
{
    private readonly static List<MessageTypeTag> allowedMessagesToDelete
        = [MessageTypeTag.ChatTextMessage, MessageTypeTag.FileMetadata, MessageTypeTag.SessionReady];

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MessageDeleteHandler"/>
    /// </summary>
    public MessageDeleteHandler(ILoggerProvider logger) : base(logger) { }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.MessageDelete];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var chatDeleteMessage = (MessageDeleteMessage)message;

        var existingMessage = context.RoomContext.Messages.GetMessageById(chatDeleteMessage.MessageIdToDelete);

        if (existingMessage == null)
        {
            LogWarning("Поступило сообщение на удаление, но его не нашлось в памяти");

            return Task.FromResult(HandlerResult.Success());
        }

        if (context.Role == Role.Host)
        {
            if (existingMessage.SenderId != message.SenderId && message.SenderId != context.SelfId)
            {
                return Task.FromResult(HandlerResult.WithErrorHandled("Сообщение можно удалить только отправителю или хосту комнаты"));
            }

            if (!allowedMessagesToDelete.Contains(existingMessage.TypeTag))
            {
                return Task.FromResult(HandlerResult.WithErrorHandled("Сообщение, которое вы хотели удалить, не подлежит удалению"));
            }
        }

        context.RoomContext.Messages.RemoveMessage(chatDeleteMessage.MessageIdToDelete);

        var replyables = context.RoomContext.Messages.GetHistory().Where(x => x is IReplyable);
        foreach (var replyableMessage in replyables)
        {
            var replyable = (IReplyable)replyableMessage;

            if (replyable.ReplyToMessageId == chatDeleteMessage.MessageIdToDelete)
            {
                replyable.ReplyToMessageId = null;
                replyable.ReplyToMessageDescription = "Сообщение было удалено";
                context.RoomContext.Messages.UpdateMessage(replyableMessage.Id, replyableMessage);
            }
        }

        return Task.FromResult(HandlerResult.WithEvent(new MessageDeletedEvent()
        {
            MessageId = chatDeleteMessage.MessageIdToDelete,
            Message = existingMessage,
            RoomId = context.RoomContext.RoomId,
        }));
    }
}
