using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Enums;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Chat.Services;

/// <inheritdoc cref="IChatSessionService"/>
public sealed class ChatSessionService : IChatSessionService
{
    private readonly IMessageRouter messageRouter;
    private readonly IChatMessageService chatMessageService;
    private readonly ISessionScanner sessionScanner;
    private readonly ISessionInstanceManager sessionInstanceManager;
    private readonly ITunnelSessionStore tunnelSessionStore;
    private readonly IIdentityService identityService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatSessionService"/>
    /// </summary>
    public ChatSessionService(IMessageRouter messageRouter,
        IChatMessageService chatMessageService,
        ISessionScanner sessionScanner,
        ISessionInstanceManager sessionInstanceManager,
        ITunnelSessionStore tunnelSessionStore,
        IIdentityService identityService)
    {
        this.messageRouter = messageRouter;
        this.chatMessageService = chatMessageService;
        this.sessionScanner = sessionScanner;
        this.sessionInstanceManager = sessionInstanceManager;
        this.tunnelSessionStore = tunnelSessionStore;
        this.identityService = identityService;
    }

    async Task IChatSessionService.SendSessionHostRequestAsync(Guid roomId, Session selectedSession, CancellationToken cancellationToken)
    {
        if (!sessionScanner.IsSessionInstalled(selectedSession.SessionId))
        {
            throw new DirectoryNotFoundException($"У вас не установлена программа для {selectedSession.Name}");
        }

        await SendAsync(new SessionHostRequestMessage()
        {
            SessionId = selectedSession.SessionId,
            SessionVersion = selectedSession.Version,
        }, roomId, cancellationToken);
    }

    async Task IChatSessionService.SendSessionJoinRequest(Guid roomId, SessionReadyMessage sessionReadyMessage, CancellationToken cancellationToken)
    {
        var session = sessionScanner.GetSessionById(sessionReadyMessage.Session.SessionId);

        switch (session)
        {
            case null when sessionReadyMessage.Session.Kind == SessionKind.Process:
                throw new DirectoryNotFoundException($"У вас не установлена программа для {sessionReadyMessage.Session.Name}");
            case null:
                session = sessionReadyMessage.Session;
                sessionScanner.RegisterAdHoc(roomId, session);
                break;
        }

        await SendAsync(new SessionJoinRequestMessage()
        {
            SessionId = session.SessionId,
            SessionVersion = session.Version,
            SubRoomId = sessionReadyMessage.SubRoomId,
        }, roomId, cancellationToken);
    }

    async Task IChatSessionService.ScanDownloadedSessions(CancellationToken cancellationToken)
        => await sessionScanner.ScanAsync(cancellationToken);

    async Task IChatSessionService.HostTunnelSessionAsync(Guid roomId, string name, int gamePort,
       int? maximumParticipants, string? version, byte[]? thumbnailData, CancellationToken cancellationToken)
    {
        if (gamePort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(gamePort), "Порт должен быть в диапазоне от 1 до 65535");
        }

        if (!Version.TryParse(version, out var parsedVersion))
        {
            parsedVersion = new Version(1, 0, 0);
        }

        var session = new Session
        {
            SessionId = $"tunnel-{Guid.NewGuid()}",
            Name = name,
            Version = parsedVersion,
            MaximumParticipants = maximumParticipants,
            Kind = SessionKind.PortTunnel,
            TunnelPort = gamePort,
            ServerExecutableFileName = "-",
            ClientExecutableFileName = "-",
            DownloadLink = string.Empty,
        };

        await tunnelSessionStore.SaveAsync(session, thumbnailData, cancellationToken);
        await sessionScanner.ScanAsync(cancellationToken);

        await SendAsync(new SessionHostRequestMessage()
        {
            SessionId = session.SessionId,
            SessionVersion = session.Version,
        }, roomId, cancellationToken);
    }

    async Task IChatSessionService.SendSessionLeaveAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken)
       => await sessionInstanceManager.LeaveAsync(
           new SessionInstanceContext(roomId, subRoomId, SessionInstanceRole.Client), cancellationToken);

    async Task IChatSessionService.SendSessionStopAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken)
        => await sessionInstanceManager.LeaveAsync(
            new SessionInstanceContext(roomId, subRoomId, SessionInstanceRole.Server), cancellationToken);

    private async Task SendAsync(IMessage? message, Guid roomId, CancellationToken cancellationToken)
    {
        if (message == null)
        {
            throw new NotImplementedException();
        }

        await messageRouter.RouteAsync(message, roomId, identityService.SelfParticipant.Id, cancellationToken);
    }
}
