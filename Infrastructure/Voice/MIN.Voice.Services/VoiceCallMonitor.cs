using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.SubRooms.Contracts.Enums;
using MIN.Core.SubRooms.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Voice.Events;
using MIN.Voice.Messaging;
using MIN.Voice.Services.Contacts.Interfaces;

namespace MIN.Voice.Services;

/// <summary>
/// Сервис для отслеживания состояния звонков
/// </summary>
public class VoiceCallMonitor : IHostedService
{
    private readonly ISubRoomManager subRoomManager;
    private readonly IEventBus eventBus;
    private readonly IMessageRouter messageRouter;
    private readonly IMuteService muteService;
    private readonly IVoicePlaybackService voicePlaybackService;
    private readonly IAudioCaptureService audioCaptureService;
    private readonly IVoiceDataTransmitter voiceDataTransmitter;
    private readonly IVoiceCallStateService voiceCallStateService;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экзепмляр <see cref="VoiceCallMonitor"/>
    /// </summary>
    public VoiceCallMonitor(ISubRoomManager subRoomManager,
        IEventBus eventBus,
        IMessageRouter messageRouter,
        IMuteService muteService,
        IVoicePlaybackService voicePlaybackService,
        IAudioCaptureService audioCaptureService,
        IVoiceDataTransmitter voiceDataTransmitter,
        IVoiceCallStateService voiceCallStateService,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.subRoomManager = subRoomManager;
        this.eventBus = eventBus;
        this.messageRouter = messageRouter;
        this.muteService = muteService;
        this.voicePlaybackService = voicePlaybackService;
        this.audioCaptureService = audioCaptureService;
        this.voiceDataTransmitter = voiceDataTransmitter;
        this.voiceCallStateService = voiceCallStateService;
        this.identityService = identityService;
        this.logger = logger;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe<RoomWentOfflineEvent>(OnRoomWentOffline);
        eventBus.Subscribe<VoiceCallEstablishedEvent>(OnVoiceCallEstablished);
        eventBus.Subscribe<VoiceCallLeftEvent>(OnVoiceCallLeft);
        eventBus.Subscribe<ParticipantLeftEvent>(OnParticipantLeft);
        return Task.CompletedTask;
    }

    private Task OnRoomWentOffline(RoomWentOfflineEvent e, CancellationToken cancellationToken)
    {
        var voiceContext = voiceCallStateService.GetRoomVoiceCallContext();

        if (voiceContext != null && e.RoomId == voiceContext.Value.RoomId)
        {
            voiceCallStateService.UnregisterVoiceCall();
            audioCaptureService.Stop();
            voiceDataTransmitter.End();
            voicePlaybackService.Clear();
        }

        return Task.CompletedTask;
    }

    private async Task OnVoiceCallEstablished(VoiceCallEstablishedEvent e, CancellationToken cancellationToken)
    {
        voiceCallStateService.RegisterVoiceCall(e.RoomId, e.SubRoomId);
        await muteService.UnmuteSelf(e.RoomId, e.SubRoomId, cancellationToken);
    }

    private Task OnVoiceCallLeft(VoiceCallLeftEvent e, CancellationToken cancellationToken)
    {
        voiceCallStateService.UnregisterVoiceCall();
        audioCaptureService.Stop();
        voiceDataTransmitter.End();

        return Task.CompletedTask;
    }

    private async Task OnParticipantLeft(ParticipantLeftEvent e, CancellationToken cancellationToken)
    {
        var roomId = e.RoomId;
        var participantId = e.Message.Participant.Id;

        var activeSubRooms = subRoomManager.GetRoomSubRooms(roomId).Where(x => x.Purpose == SubRoomPurpose.Voice && x.IsActive);
        foreach (var subRoom in activeSubRooms)
        {
            if (subRoomManager.IsInSubRoom(roomId, subRoom.Id, participantId))
            {
                var isLast = !subRoomManager.LeaveSubRoom(roomId, subRoom.Id, participantId);

                await messageRouter.RouteAsync(new VoiceParticipantLeftMessage()
                {
                    SubRoomId = subRoom.Id,
                    Participant = e.Message.Participant,
                }, roomId, identityService.SelfParticipant.Id, cancellationToken);

                if (isLast)
                {
                    await messageRouter.RouteAsync(new VoiceCallEndedMessage()
                    {
                        SubRoomId = subRoom.Id,
                    }, roomId, identityService.SelfParticipant.Id, cancellationToken);
                }
            }
        }
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        audioCaptureService.Stop();
        voiceDataTransmitter.End();
        voicePlaybackService.Dispose();
        return Task.CompletedTask;
    }
}
