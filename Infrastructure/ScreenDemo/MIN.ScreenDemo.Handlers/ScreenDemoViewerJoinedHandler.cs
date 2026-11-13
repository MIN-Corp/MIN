using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.Handlers;

internal sealed class ScreenDemoViewerJoinedHandler : BaseHandler
{
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IScreenDemoTransmitter screenDemoTransmitter;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoViewerJoinedHandler"/>
    /// </summary>
    public ScreenDemoViewerJoinedHandler(IScreenDemoStateService screenDemoStateService,
        IScreenDemoTransmitter screenDemoTransmitter,
        ILoggerProvider logger) : base(logger)
    {
        this.screenDemoStateService = screenDemoStateService;
        this.screenDemoTransmitter = screenDemoTransmitter;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.ScreenDemoViewerJoined];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var viewerJoinedMessage = (ScreenDemoViewerJoinedMessage)message;

        var roomId = context.RoomContext.RoomId;
        var subRoomId = viewerJoinedMessage.SubRoomId;
        var participant = viewerJoinedMessage.Participant;

        screenDemoStateService.AddViewer(roomId, subRoomId, participant.Id);

        var ownDemo = screenDemoStateService.GetOwnDemo(roomId, context.SelfId);

        // Это моя демонстрация — зритель присоединился, начинаю отправку кадров
        if (ownDemo != null && ownDemo.SubRoomId == subRoomId && context.SelfId != participant.Id)
        {
            screenDemoTransmitter.AddViewer(participant.Id);
        }

        return Task.FromResult(HandlerResult.WithEvent(new ScreenDemoViewerJoinedEvent()
        {
            RoomId = roomId,
            SubRoomId = subRoomId,
            ViewerId = participant.Id,
        }));
    }
}
