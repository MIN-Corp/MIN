using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Models.SubRooms;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;
using MIN.Voice.Events;

namespace MIN.Chat.Services;

/// <inheritdoc cref="IChatScreenDemoService"/>
public sealed class ChatScreenDemoService : IChatScreenDemoService
{
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IScreenDemoPlaybackService screenDemoPlaybackService;
    private readonly IScreenDemoTransmitter screenDemoTransmitter;
    private readonly IMessageRouter messageRouter;
    private readonly IIdentityService identityService;

    /// <summary>
    /// Источник, выбранный в диалоге, который ждёт подтверждения демонстрации (roomId -> источник)
    /// </summary>
    private readonly Dictionary<Guid, ScreenSourceInfo> pendingSources = [];

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatScreenDemoService"/>
    /// </summary>
    public ChatScreenDemoService(IScreenDemoStateService screenDemoStateService,
        IScreenDemoPlaybackService screenDemoPlaybackService,
        IScreenDemoTransmitter screenDemoTransmitter,
        IMessageRouter messageRouter,
        IEventBus eventBus,
        IIdentityService identityService)
    {
        this.screenDemoStateService = screenDemoStateService;
        this.screenDemoPlaybackService = screenDemoPlaybackService;
        this.screenDemoTransmitter = screenDemoTransmitter;
        this.messageRouter = messageRouter;
        this.identityService = identityService;

        eventBus.Subscribe<ScreenDemoEstablishedEvent>(OnDemoEstablished);
        eventBus.Subscribe<VoiceCallLeftEvent>(OnVoiceCallLeft);
    }

    async Task IChatScreenDemoService.RequestDemoStateAsync(Guid roomId, CancellationToken cancellationToken)
        => await SendAsync(new ScreenDemoStateRequestMessage(), roomId, cancellationToken);

    async Task IChatScreenDemoService.StartDemoAsync(Guid roomId, ScreenSourceInfo source, CancellationToken cancellationToken)
    {
        pendingSources[roomId] = source;

        await SendAsync(new ScreenDemoStartRequestMessage(), roomId, cancellationToken);
    }

    async Task IChatScreenDemoService.JoinDemoAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken)
        => await SendAsync(new ScreenDemoJoinRequestMessage()
        {
            SubRoomId = subRoomId,
        }, roomId, cancellationToken);

    async Task IChatScreenDemoService.LeaveDemoAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken)
    {
        // локально чистим себя сразу, не дожидаясь от хоста
        var demo = screenDemoStateService.GetDemo(roomId, subRoomId);

        if (demo != null)
        {
            screenDemoPlaybackService.RemoveStream(roomId, demo.Sharer.Id);
        }

        screenDemoStateService.RemoveViewer(roomId, subRoomId, identityService.SelfParticipant.Id);

        await SendAsync(new ScreenDemoLeaveMessage()
        {
            SubRoomId = subRoomId,
        }, roomId, cancellationToken);
    }

    async Task IChatScreenDemoService.EndDemoAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken)
    {
        // transmitter.End() и остановка предпросмотра произойдут
        // в ScreenDemoEndHandler (RequiresLocalDuplication = true)
        await SendAsync(new ScreenDemoEndMessage()
        {
            SubRoomId = subRoomId,
        }, roomId, cancellationToken);
    }

    private Task OnDemoEstablished(ScreenDemoEstablishedEvent e, CancellationToken cancellationToken)
    {
        var selfId = identityService.SelfParticipant.Id;

        if (e.SharerId == selfId)
        {
            // Подтверждена моя демонстрация — начинаю захват и локальный предпросмотр
            if (pendingSources.Remove(e.RoomId, out var source))
            {
                screenDemoTransmitter.Begin(new SubRoomContext(e.RoomId, e.SubRoomId), source);
            }
        }
        else
        {
            // Я присоединился как зритель — начинаю приём потока
            screenDemoPlaybackService.AddStream(e.RoomId, e.SharerId);
        }

        return Task.CompletedTask;
    }

    private async Task OnVoiceCallLeft(VoiceCallLeftEvent e, CancellationToken cancellationToken)
    {
        // Требование: демонстрация возможна только во время звонка.
        // Ушёл из звонка — заканчиваю свою демонстрацию и выхожу из всех чужих
        var selfId = identityService.SelfParticipant.Id;

        var ownDemo = screenDemoStateService.GetOwnDemo(e.RoomId, selfId);

        if (ownDemo != null)
        {
            await ((IChatScreenDemoService)this).EndDemoAsync(e.RoomId, ownDemo.SubRoomId, cancellationToken);
        }

        foreach (var demo in screenDemoStateService.GetActiveDemos(e.RoomId).Where(d => d.ViewerIds.Contains(selfId)))
        {
            await ((IChatScreenDemoService)this).LeaveDemoAsync(e.RoomId, demo.SubRoomId, cancellationToken);
        }
    }

    private async Task SendAsync(IMessage message, Guid roomId, CancellationToken cancellationToken)
        => await messageRouter.RouteAsync(message, roomId, identityService.SelfParticipant.Id, cancellationToken);
}
