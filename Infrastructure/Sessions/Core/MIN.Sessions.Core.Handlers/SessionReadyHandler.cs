using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Enums;
using MIN.Sessions.Core.Services.Contracts.Interfaces;

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionReadyHandler : BaseHandler
{
    private readonly ISessionScanner sessionScanner;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="SessionReadyHandler"/>
    /// </summary>
    public SessionReadyHandler(ISessionScanner sessionScanner,
        ILoggerProvider logger) : base(logger)
    {
        this.sessionScanner = sessionScanner;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.SessionReady];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var sessionReadyMessage = (SessionReadyMessage)message;

        if (sessionReadyMessage.Session.Kind == SessionKind.PortTunnel
            && !sessionScanner.IsSessionInstalled(sessionReadyMessage.Session.SessionId))
        {
            sessionScanner.RegisterAdHoc(context.RoomContext.RoomId, sessionReadyMessage.Session);
        }

        context.RoomContext.Messages.AddMessage(sessionReadyMessage);

        return Task.FromResult(HandlerResult.WithEvent(new SessionReadyMessageReceivedEvent()
        {
            Message = sessionReadyMessage,
            RoomId = context.RoomContext.RoomId,
        }));
    }
}
