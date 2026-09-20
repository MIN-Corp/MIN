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
    private readonly static List<MessageTypeTag> allowedMessagesToDelete = [MessageTypeTag.ChatTextMessage, MessageTypeTag.FileMetadata];

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="MessageDeleteHandler"/>
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

            if (context.Role == Role.Host)
            {
                return Task.FromResult(HandlerResult.WithErrorHandled("Сообщение, которое вы хотели удалить, не найдено"));
            }

            return Task.FromResult(HandlerResult.Success());
        }

        if (context.Role == Role.Host)
        {
            if (existingMessage.SenderId != message.SenderId)
            {
                return Task.FromResult(HandlerResult.WithErrorHandled("Сообщение, которое вы хотели удалить, было отправлено не вами"));
            }

            if (!allowedMessagesToDelete.Contains(existingMessage.TypeTag))
            {
                return Task.FromResult(HandlerResult.WithErrorHandled("Сообщение, которое вы хотели удалить, не подлежит удалению"));
            }
        }

        context.RoomContext.Messages.RemoveMessage(chatDeleteMessage.MessageIdToDelete);

        var replyables = context.RoomContext.Messages.GetHistory().OfType<IReplyable>();
        foreach (var replyable in replyables)
        {
            if (replyable.ReplyToMessageId == chatDeleteMessage.MessageIdToDelete)
            {
                replyable.ReplyToMessageId = null;
                replyable.ReplyToMessageDescription = "Сообщение было удалено";
            }
        }

        return Task.FromResult(HandlerResult.WithEvent(new MessageDeletedEvent()
        {
            MessageId = chatDeleteMessage.MessageIdToDelete,
            RoomId = context.RoomContext.RoomId,
        }));
    }
}
