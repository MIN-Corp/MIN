using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.Handlers;

internal sealed class ScreenDemoAudioDataHandler : BaseHandler
{
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IScreenDemoPlaybackService screenDemoPlaybackService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoAudioDataHandler"/>
    /// </summary>
    public ScreenDemoAudioDataHandler(IScreenDemoStateService screenDemoStateService,
        IScreenDemoPlaybackService screenDemoPlaybackService,
        ILoggerProvider logger) : base(logger)
    {
        this.screenDemoStateService = screenDemoStateService;
        this.screenDemoPlaybackService = screenDemoPlaybackService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.ScreenDemoAudioData];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var audioData = (ScreenDemoAudioDataMessage)message;

        var roomId = context.RoomContext.RoomId;
        var sharerId = message.SenderId;

        if (sharerId == context.SelfId)
        {
            return Task.FromResult(HandlerResult.Success());
        }

        if (!screenDemoStateService.IsWatching(roomId, audioData.SubRoomId, context.SelfId))
        {
            return Task.FromResult(HandlerResult.Success());
        }

        screenDemoPlaybackService.SubmitAudioSamples(roomId, sharerId, audioData.SequenceNumber, audioData.TimestampMs, audioData.Data);

        return Task.FromResult(HandlerResult.Success());
    }
}
