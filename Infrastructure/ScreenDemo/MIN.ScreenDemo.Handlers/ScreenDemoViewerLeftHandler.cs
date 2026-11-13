using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.Handlers;

internal sealed class ScreenDemoViewerLeftHandler : BaseHandler
{
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IScreenDemoTransmitter screenDemoTransmitter;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoViewerLeftHandler"/>
    /// </summary>
    public ScreenDemoViewerLeftHandler(IScreenDemoStateService screenDemoStateService,
        IScreenDemoTransmitter screenDemoTransmitter,
        ILoggerProvider logger) : base(logger)
    {
        this.screenDemoStateService = screenDemoStateService;
        this.screenDemoTransmitter = screenDemoTransmitter;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.ScreenDemoViewerLeft];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var viewerLeftMessage = (ScreenDemoViewerLeftMessage)message;

        var roomId = context.RoomContext.RoomId;
        var subRoomId = viewerLeftMessage.SubRoomId;
        var participant = viewerLeftMessage.Participant;

        screenDemoStateService.RemoveViewer(roomId, subRoomId, participant.Id);

        var ownDemo = screenDemoStateService.GetOwnDemo(roomId, context.SelfId);

        // Это моя демонстрация — зрителя больше нет, возможно прекращаю отправку
        if (ownDemo != null && ownDemo.SubRoomId == subRoomId && context.SelfId != participant.Id)
        {
            screenDemoTransmitter.RemoveViewer(participant.Id);
        }

        return Task.FromResult(HandlerResult.WithEvent(new ScreenDemoViewerLeftEvent()
        {
            RoomId = roomId,
            SubRoomId = subRoomId,
            ViewerId = participant.Id,
        }));
    }
}
