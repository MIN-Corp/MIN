using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.Handlers;

internal sealed class ScreenDemoDataHandler : BaseHandler
{
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IScreenDemoPlaybackService screenDemoPlaybackService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoDataHandler"/>
    /// </summary>
    public ScreenDemoDataHandler(IScreenDemoStateService screenDemoStateService,
        IScreenDemoPlaybackService screenDemoPlaybackService,
        ILoggerProvider logger) : base(logger)
    {
        this.screenDemoStateService = screenDemoStateService;
        this.screenDemoPlaybackService = screenDemoPlaybackService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.ScreenDemoFrameData];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var frameData = (ScreenDemoFrameDataMessage)message;

        var roomId = context.RoomContext.RoomId;
        var subRoomId = frameData.SubRoomId;
        var sharerId = message.SenderId;

        // Свои кадры не приходят (RequiresLocalDuplication = false),
        // но проверка не помешает
        if (sharerId == context.SelfId)
        {
            return Task.FromResult(HandlerResult.Success());
        }

        // Широковещательная рассылка доходит до всех — не смотрящие молча отбрасывают
        if (!screenDemoStateService.IsWatching(roomId, subRoomId, context.SelfId))
        {
            return Task.FromResult(HandlerResult.Success());
        }

        screenDemoPlaybackService.SubmitFragment(
            roomId,
            sharerId,
            frameData.FrameId,
            frameData.FragmentIndex,
            frameData.FragmentCount,
            frameData.IsKeyframe,
            frameData.Width,
            frameData.Height,
            frameData.Data);

        return Task.FromResult(HandlerResult.WithEvent(new ScreenDemoDataReceivedEvent()
        {
            RoomId = roomId,
            SharerId = sharerId,
        }));
    }
}
