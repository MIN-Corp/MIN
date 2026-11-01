using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.Handlers;

internal sealed class ScreenDemoEndHandler : BaseHandler
{
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IScreenDemoPlaybackService screenDemoPlaybackService;
    private readonly IScreenDemoTransmitter screenDemoTransmitter;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoEndHandler"/>
    /// </summary>
    public ScreenDemoEndHandler(IScreenDemoStateService screenDemoStateService,
        IScreenDemoPlaybackService screenDemoPlaybackService,
        IScreenDemoTransmitter screenDemoTransmitter,
        ILoggerProvider logger) : base(logger)
    {
        this.screenDemoStateService = screenDemoStateService;
        this.screenDemoPlaybackService = screenDemoPlaybackService;
        this.screenDemoTransmitter = screenDemoTransmitter;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.ScreenDemoEnd];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var screenDemoEndMessage = (ScreenDemoEndMessage)message;

        var roomId = context.RoomContext.RoomId;
        var subRoomId = screenDemoEndMessage.SubRoomId;
        var sharerId = message.SenderId;

        // Хост деактивирует подкомнату. Инициатор подкомнаты — сам демонстрирующий,
        // так что TryStopSubRoom его пропустит
        if (context.Role == Role.Host)
        {
            context.RoomContext.SubRooms.TryStopSubRoom(subRoomId, sharerId);
        }

        var ownDemo = screenDemoStateService.GetOwnDemo(roomId, context.SelfId);

        if (ownDemo != null && ownDemo.SubRoomId == subRoomId)
        {
            // Это моя демонстрация — полностью останавливаю передачу
            screenDemoTransmitter.End();
        }

        if (screenDemoStateService.IsWatching(roomId, subRoomId, context.SelfId))
        {
            screenDemoPlaybackService.RemoveStream(roomId, sharerId);
        }

        screenDemoStateService.RemoveDemo(roomId, subRoomId);

        return Task.FromResult(HandlerResult.WithEvent(new ScreenDemoEndedEvent()
        {
            RoomId = roomId,
            SubRoomId = subRoomId,
            SharerId = sharerId,
        }));
    }
}
