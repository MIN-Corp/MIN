using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Models.SubRooms;
using MIN.Voice.Messaging;
using MIN.Voice.Services.Contacts.Interfaces;

namespace MIN.Voice.Services;

/// <inheritdoc cref="IMuteService"/>
public class MuteService : IMuteService
{
    private readonly IAudioCaptureService audioCaptureService;
    private readonly IVoiceDataTransmitter voiceDataTransmitter;
    private readonly IVoicePlaybackService voicePlaybackService;
    private readonly IMessageRouter messageRouter;
    private readonly IIdentityService identityService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MuteService"/>
    /// </summary>
    public MuteService(IAudioCaptureService audioCaptureService,
        IVoiceDataTransmitter voiceDataTransmitter,
        IVoicePlaybackService voicePlaybackService,
        IMessageRouter messageRouter,
        IIdentityService identityService)
    {
        this.audioCaptureService = audioCaptureService;
        this.voiceDataTransmitter = voiceDataTransmitter;
        this.voicePlaybackService = voicePlaybackService;
        this.messageRouter = messageRouter;
        this.identityService = identityService;
    }

    async Task IMuteService.MuteSelf(Guid roomId, int subroomId, CancellationToken cancellationToken)
    {
        var context = new SubRoomContext(roomId, subroomId);
        await SendMutedState(context, muted: true, cancellationToken);
        audioCaptureService.Stop();
        voiceDataTransmitter.End();
    }

    async Task IMuteService.UnmuteSelf(Guid roomId, int subroomId, CancellationToken cancellationToken)
    {
        var context = new SubRoomContext(roomId, subroomId);
        await SendMutedState(context, muted: false, cancellationToken);
        audioCaptureService.Start();
        voiceDataTransmitter.Begin(context);
    }

    private async Task SendMutedState(SubRoomContext subRoomContext, bool muted, CancellationToken cancellationToken)
        => await messageRouter.RouteAsync(new VoiceMuteStateChangedMessage()
        {
            SubRoomId = subRoomContext.SubRoomId,
            IsMuted = muted
        }, subRoomContext.RoomId, identityService.SelfParticipant.Id, cancellationToken);

    void IMuteService.MuteParticipant(Guid participantId)
    {
        voicePlaybackService.RemoveParticipant(participantId);
    }

    void IMuteService.UnmuteParticipant(Guid participantId)
    {
        voicePlaybackService.AddParticipant(participantId);
    }
}
