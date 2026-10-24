using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Messaging.Contracts.Enums;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionSpecificHandler : BaseHandler
{
    private readonly ISessionInstanceManager sessionInstanceManager;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="SessionSpecificHandler"/>
    /// </summary>
    public SessionSpecificHandler(ISessionInstanceManager sessionInstanceManager,
        ILoggerProvider logger) : base(logger)
    {
        this.sessionInstanceManager = sessionInstanceManager;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.SessionSpecific];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var sessionSpecificMessage = (SessionSpecificMessage)message;

        var roomId = context.RoomContext.RoomId;
        var subRoomId = sessionSpecificMessage.SubRoomId;

        if (sessionSpecificMessage.SessionInstanceRole == SessionInstanceRole.Server
            && message.RecipientId != context.SelfId && !message.IsPublic)
        {
            return HandlerResult.Success();
        }

        var instances = sessionInstanceManager.GetInstances(roomId, subRoomId);

        if (sessionSpecificMessage.Route == SessionMessageRoute.Direct)
        {
            // Direct: отправить всем локальным экземплярам подкомнаты

            foreach (var instance in instances)
            {
                await instance.SendDataAsync(sessionSpecificMessage.Body, message.SenderId, context.CancellationToken);
            }
            return HandlerResult.Success(stopPropagation: true);
        }

        var isClientRequest = sessionSpecificMessage.SessionInstanceRole == SessionInstanceRole.Client;

        // ViaServer: role-swap логика

        var processContext = new SessionInstanceContext(roomId, subRoomId,
            isClientRequest ? SessionInstanceRole.Server : SessionInstanceRole.Client);

        var target = instances.FirstOrDefault(x => x.Context == processContext);

        if (target != null)
        {
            await target.SendDataAsync(sessionSpecificMessage.Body, message.SenderId, context.CancellationToken);
        }

        return HandlerResult.Success(stopPropagation: isClientRequest);
    }
}
