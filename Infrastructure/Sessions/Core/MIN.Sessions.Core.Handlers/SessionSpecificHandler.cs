using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Messaging.Contracts.Enums;
using MIN.Sessions.Core.Messaging.Ipc;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionSpecificHandler : BaseHandler
{
    private readonly ISessionProcessBridge sessionProcessBridge;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="SessionSpecificHandler"/>
    /// </summary>
    public SessionSpecificHandler(ISessionProcessBridge sessionProcessBridge,
        ILoggerProvider logger) : base(logger)
    {
        this.sessionProcessBridge = sessionProcessBridge;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.SessionSpecific];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var sessionSpecificMessage = (SessionSpecificMessage)message;

        var roomId = context.RoomContext.RoomId;
        var subRoomId = sessionSpecificMessage.SubRoomId;

        if (sessionSpecificMessage.SessionProcessRole == SessionInstanceRole.Server
            && message.RecipientId != context.SelfId && !message.IsPublic)
        {
            return HandlerResult.Success();
        }

        var connections = sessionProcessBridge.GetConnections(roomId, subRoomId);

        if (sessionSpecificMessage.Route == SessionMessageRoute.Direct)
        {
            // Direct: отправить всем локальным процессам подкомнаты

            foreach (var conn in connections)
            {
                await sessionProcessBridge.SendIpcMessage(
                    new InSessionMessage(sessionSpecificMessage.Body), conn, message.SenderId, context.CancellationToken);
            }
            return HandlerResult.Success(stopPropagation: true);
        }

        var isClientRequest = sessionSpecificMessage.SessionProcessRole == SessionInstanceRole.Client;

        // ViaServer: role-swap логика

        var processContext = new SessionInstanceContext(roomId, subRoomId,
            isClientRequest ? SessionInstanceRole.Server : SessionInstanceRole.Client);

        if (connections.Contains(processContext))
        {
            await sessionProcessBridge.SendIpcMessage(new InSessionMessage(sessionSpecificMessage.Body),
                processContext, message.SenderId, context.CancellationToken);
        }

        return HandlerResult.Success(stopPropagation: isClientRequest);
    }
}
