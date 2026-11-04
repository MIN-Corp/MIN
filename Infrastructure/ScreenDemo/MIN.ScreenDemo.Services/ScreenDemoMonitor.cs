using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.Services;

/// <summary>
/// Сервис отслеживания жизненного цикла демонстраций экрана:
/// чистит подкомнаты и состояние при выходах участников, оффлайне и остановке приложения
/// </summary>
public class ScreenDemoMonitor : IHostedService
{
    private readonly IRoomFactory roomFactory;
    private readonly IEventBus eventBus;
    private readonly IMessageRouter messageRouter;
    private readonly IScreenDemoTransmitter screenDemoTransmitter;
    private readonly IScreenDemoPlaybackService screenDemoPlaybackService;
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    int IHostedService.Priority => 0;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoMonitor"/>
    /// </summary>
    public ScreenDemoMonitor(IRoomFactory roomFactory,
        IEventBus eventBus,
        IMessageRouter messageRouter,
        IScreenDemoTransmitter screenDemoTransmitter,
        IScreenDemoPlaybackService screenDemoPlaybackService,
        IScreenDemoStateService screenDemoStateService,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.roomFactory = roomFactory;
        this.eventBus = eventBus;
        this.messageRouter = messageRouter;
        this.screenDemoTransmitter = screenDemoTransmitter;
        this.screenDemoPlaybackService = screenDemoPlaybackService;
        this.screenDemoStateService = screenDemoStateService;
        this.identityService = identityService;
        this.logger = logger;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe<RoomWentOfflineEvent>(OnRoomWentOffline);
        eventBus.Subscribe<ParticipantLeftEvent>(OnParticipantLeft);
        return Task.CompletedTask;
    }

    private async Task OnRoomWentOffline(RoomWentOfflineEvent e, CancellationToken cancellationToken)
    {
        var ownDemo = screenDemoStateService.GetOwnDemo(e.RoomId, identityService.SelfParticipant.Id);

        if (ownDemo != null)
        {
            screenDemoTransmitter.End();

            await messageRouter.PublishLocally(new ScreenDemoEndMessage()
            {
                SubRoomId = ownDemo.SubRoomId,
                SenderId = identityService.SelfParticipant.Id,
            }, e.RoomId, null, cancellationToken);
        }

        screenDemoPlaybackService.Clear();
        screenDemoStateService.Clear(e.RoomId);
    }

    private async Task OnParticipantLeft(ParticipantLeftEvent e, CancellationToken cancellationToken)
    {
        var roomId = e.RoomId;
        var participantId = e.Message.Participant.Id;
        var context = roomFactory.GetOrCreateContext(roomId);

        var activeDemos = context.SubRooms.GetRoomSubRooms()
            .Where(x => x.Purpose == SubRoomPurpose.ScreenDemo && x.IsActive);

        foreach (var subRoom in activeDemos)
        {
            if (subRoom.CreatorId == participantId)
            {
                // демонстрирующий покинул комнату — его демонстрация завершается для всех
                await messageRouter.RouteAsync(new ScreenDemoEndMessage()
                {
                    SubRoomId = subRoom.Id,
                }, roomId, identityService.SelfParticipant.Id, cancellationToken);

                context.SubRooms.RemoveSubRoom(subRoom.Id);
            }
            else if (context.SubRooms.IsInSubRoom(subRoom.Id, participantId))
            {
                // зритель покинул комнату — убираем его из демонстрации
                context.SubRooms.LeaveSubRoom(subRoom.Id, participantId);

                await messageRouter.RouteAsync(new ScreenDemoViewerLeftMessage()
                {
                    SubRoomId = subRoom.Id,
                    Participant = e.Message.Participant,
                }, roomId, identityService.SelfParticipant.Id, cancellationToken);
            }
        }
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        foreach (var context in roomFactory.GetAllContexts())
        {
            var activeDemos = context.SubRooms.GetRoomSubRooms()
                .Where(x => x.Purpose == SubRoomPurpose.ScreenDemo && x.IsActive)
                .ToList();

            foreach (var subRoom in activeDemos)
            {
                context.SubRooms.RemoveSubRoom(subRoom.Id);
            }
        }

        screenDemoTransmitter.End();
        screenDemoPlaybackService.Dispose();

        return Task.CompletedTask;
    }
}
