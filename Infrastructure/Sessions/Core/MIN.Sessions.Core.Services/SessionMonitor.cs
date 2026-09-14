using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.SubRooms.Contracts.Enums;
using MIN.Core.SubRooms.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services;

/// <summary>
/// Сервис для отслеживания состояния сессий
/// </summary>
public class SessionMonitor : IHostedService
{
    private readonly ISubRoomManager subRoomManager;
    private readonly IEventBus eventBus;
    private readonly IMessageRouter messageRouter;
    private readonly ISessionScanner sessionScanner;
    private readonly ISessionProcessManager sessionProcessManager;
    private readonly ISessionProcessBridge sessionProcessBridge;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экзепмляр <see cref="SessionMonitor"/>
    /// </summary>
    public SessionMonitor(ISubRoomManager subRoomManager,
        IEventBus eventBus,
        IMessageRouter messageRouter,
        ISessionScanner sessionScanner,
        ISessionProcessManager sessionProcessManager,
        ISessionProcessBridge sessionProcessBridge,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.subRoomManager = subRoomManager;
        this.eventBus = eventBus;
        this.messageRouter = messageRouter;
        this.sessionScanner = sessionScanner;
        this.sessionProcessManager = sessionProcessManager;
        this.sessionProcessBridge = sessionProcessBridge;
        this.identityService = identityService;
        this.logger = logger;
    }

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        await sessionScanner.ScanAsync(cancellationToken);
        await sessionProcessBridge.StartListeningAsync(cancellationToken);

        eventBus.Subscribe<RoomWentOfflineEvent>(WentOffline);
        eventBus.Subscribe<ParticipantLeftEvent>(OnParticipantLeft);
        eventBus.Subscribe<SessionDeactivatedEvent>(OnSessionDeactivated);
    }

    private async Task WentOffline(RoomWentOfflineEvent e, CancellationToken cancellationToken)
        => await sessionProcessManager.StopForRoomAsync(e.RoomId);

    private async Task OnSessionDeactivated(SessionDeactivatedEvent e, CancellationToken cancellationToken)
        => await sessionProcessManager.StopAsync(new ProcessContext(e.RoomId, e.SubRoomId, SessionProcessRole.Server));

    private async Task OnParticipantLeft(ParticipantLeftEvent e, CancellationToken cancellationToken)
    {
        var roomId = e.RoomId;
        var participantId = e.Message.Participant.Id;

        var activeSubRooms = subRoomManager.GetRoomSubRooms(roomId).Where(x => x.Purpose == SubRoomPurpose.Activity && x.IsActive);
        foreach (var subRoom in activeSubRooms)
        {
            if (subRoomManager.IsInSubRoom(roomId, subRoom.Id, participantId))
            {
                var isLast = !subRoomManager.LeaveSubRoom(roomId, subRoom.Id, participantId);

                var leaveMessage = new SessionParticipantLeftMessage()
                {
                    SubRoomId = subRoom.Id,
                    Participant = e.Message.Participant,
                    IsLast = isLast
                };

                await messageRouter.RouteAsync(leaveMessage, roomId, identityService.SelfParticipant.Id, cancellationToken);
            }
        }
    }

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        await sessionProcessBridge.StopListeningAsync(cancellationToken);
        await sessionProcessManager.StopAllAsync();
    }
}
