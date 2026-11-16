using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Handlers;

/// <summary>
/// Обработчик старта демонстрации экрана: регистрирует демонстрацию в состоянии
/// на всех машинах и публикует событие для UI
/// </summary>
internal sealed class ScreenDemoStartedHandler : BaseHandler
{
    private readonly IScreenDemoStateService screenDemoStateService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoStartedHandler"/>
    /// </summary>
    public ScreenDemoStartedHandler(IScreenDemoStateService screenDemoStateService, ILoggerProvider logger) : base(logger)
    {
        this.screenDemoStateService = screenDemoStateService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.ScreenDemoStarted];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var startedMessage = (ScreenDemoStartedMessage)message;
        var roomId = context.RoomContext.RoomId;

        // Регистрируем демонстрацию в состоянии на КАЖДОЙ машине (на хосте
        // ScreenDemoStartHandler уже делал то же самое — перезапись идемпотентна)
        screenDemoStateService.AddDemo(new ScreenDemoSessionInfo(
            roomId,
            startedMessage.SubRoomId,
            startedMessage.Sharer,
            []));

        return Task.FromResult(HandlerResult.WithEvent(new ScreenDemoStartedEvent()
        {
            RoomId = roomId,
            SubRoomId = startedMessage.SubRoomId,
            SharerId = startedMessage.Sharer.Id,
        }));
    }
}
