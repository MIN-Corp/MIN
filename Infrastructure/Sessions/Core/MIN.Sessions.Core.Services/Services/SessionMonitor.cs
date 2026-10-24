using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Services;

/// <summary>
/// Сервис для отслеживания состояния сессий
/// </summary>
public class SessionMonitor : IHostedService
{
    private readonly IEventBus eventBus;
    private readonly IRoomFactory roomFactory;
    private readonly IMessageRouter messageRouter;
    private readonly ISessionScanner sessionScanner;
    private readonly ISessionProcessBridge sessionProcessBridge;
    private readonly ISessionInstanceManager sessionInstanceManager;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    int IHostedService.Priority => 0;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SessionMonitor"/>
    /// </summary>
    public SessionMonitor(IEventBus eventBus,
        IRoomFactory roomFactory,
        IMessageRouter messageRouter,
        ISessionScanner sessionScanner,
        ISessionProcessBridge sessionProcessBridge,
        ISessionInstanceManager sessionInstanceManager,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.eventBus = eventBus;
        this.roomFactory = roomFactory;
        this.messageRouter = messageRouter;
        this.sessionScanner = sessionScanner;
        this.sessionProcessBridge = sessionProcessBridge;
        this.sessionInstanceManager = sessionInstanceManager;
        this.identityService = identityService;
        this.logger = logger;
    }

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        await sessionScanner.ScanAsync(cancellationToken);
        await sessionProcessBridge.StartListeningAsync(cancellationToken);

        eventBus.Subscribe<RoomWentOfflineEvent>(WentOffline);
        eventBus.Subscribe<ParticipantLeftEvent>(OnParticipantLeft);
        eventBus.Subscribe<MessageDeletedEvent>(OnMessageDeleted);
        eventBus.Subscribe<SessionDeactivatedEvent>(OnSessionDeactivated);
    }

    private async Task OnMessageDeleted(MessageDeletedEvent e, CancellationToken cancellationToken)
    {
        if (e.Message is SessionReadyMessage sessionReady)
        {
            await sessionInstanceManager.LeaveAsync(new SessionInstanceContext(e.RoomId, sessionReady.SubRoomId, SessionInstanceRole.Server),
                cancellationToken);
        }
    }

    private async Task WentOffline(RoomWentOfflineEvent e, CancellationToken cancellationToken)
    {
        await sessionInstanceManager.StopForRoomAsync(e.RoomId);
        sessionScanner.UnregisterAdHocForRoom(e.RoomId);

        var roomContext = roomFactory.GetOrCreateContext(e.RoomId);

        foreach (var subRoom in roomContext.SubRooms.GetRoomSubRooms().Where(x => x.IsActive).ToList())
        {
            var readyMessage = roomContext.Messages.GetHistory()
                .OfType<SessionReadyMessage>()
                .FirstOrDefault(x => x.SubRoomId == subRoom.Id);

            if (readyMessage != null)
            {
                readyMessage.CurrentParticipantAmount = 0;
                roomContext.Messages.UpdateMessage(readyMessage.Id, readyMessage);
            }

            await eventBus.PublishAsync(new SessionDeactivatedEvent()
            {
                RoomId = e.RoomId,
                SubRoomId = subRoom.Id,
            }, cancellationToken).ConfigureAwait(false);

            roomContext.SubRooms.TryStopSubRoom(subRoom.Id, identityService.SelfParticipant.Id);
        }
    }

    private async Task OnSessionDeactivated(SessionDeactivatedEvent e, CancellationToken cancellationToken)
        => await sessionInstanceManager.StopAsync(new SessionInstanceContext(e.RoomId, e.SubRoomId, SessionInstanceRole.Server));

    private async Task OnParticipantLeft(ParticipantLeftEvent e, CancellationToken cancellationToken)
    {
        var roomId = e.RoomId;
        var participantId = e.Message.Participant.Id;

        var context = roomFactory.GetOrCreateContext(roomId);

        var activeSubRooms = context.SubRooms.GetRoomSubRooms().Where(x => x.Purpose == SubRoomPurpose.Activity && x.IsActive);
        foreach (var subRoom in activeSubRooms)
        {
            if (!context.SubRooms.IsInSubRoom(subRoom.Id, participantId))
            {
                continue;
            }

            var isLast = !context.SubRooms.LeaveSubRoom(subRoom.Id, participantId);

            var leaveMessage = new SessionParticipantLeftMessage()
            {
                SubRoomId = subRoom.Id,
                Participant = e.Message.Participant,
                IsLast = isLast
            };

            await messageRouter.RouteAsync(leaveMessage, roomId, identityService.SelfParticipant.Id, cancellationToken);
        }
    }

    private void EndAllSessions()
    {
        var contexts = roomFactory.GetAllContexts();
        foreach (var context in contexts)
        {
            var activeSubRooms = context.SubRooms.GetRoomSubRooms().Where(x => x.Purpose == SubRoomPurpose.Activity && x.IsActive).ToList();
            foreach (var subRoom in activeSubRooms)
            {
                var existingSessionReadyMessageId = context.Messages.GetHistory()
                    .OfType<SessionReadyMessage>().FirstOrDefault(x => x.SubRoomId == subRoom.Id)?.Id;

                if (existingSessionReadyMessageId != null)
                {
                    var existing = context.Messages.GetMessageById(existingSessionReadyMessageId.Value) as SessionReadyMessage;
                    existing!.CurrentParticipantAmount = 0;
                    context.Messages.UpdateMessage(existing.Id, existing);
                }

                context.SubRooms.TryStopSubRoom(subRoom.Id, identityService.SelfParticipant.Id);
            }
        }
    }

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        EndAllSessions();

        await sessionProcessBridge.StopListeningAsync(cancellationToken);
        await sessionInstanceManager.StopAllAsync();
    }
}
