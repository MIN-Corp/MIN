using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Handlers;

internal sealed class ScreenDemoStateHandler : BaseHandler
{
    private readonly IScreenDemoStateService screenDemoStateService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoStateHandler"/>
    /// </summary>
    public ScreenDemoStateHandler(ILoggerProvider logger,
        IScreenDemoStateService screenDemoStateService) : base(logger)
    {
        this.screenDemoStateService = screenDemoStateService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.ScreenDemoStateRequest, MessageTypeTag.ScreenDemoStateResponse];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        switch (message)
        {
            case ScreenDemoStateRequestMessage _:
            {
                if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out _))
                {
                    return Task.FromResult(HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true));
                }

                if (context.Role != Role.Host)
                {
                    return Task.FromResult(HandlerResult.Failure($"Получил сообщение {message.GetType()} в {nameof(ScreenDemoStateHandler)} как {context.Role}, хотя не должен был", stopPropagation: false));
                }

                var activeDemos = context.RoomContext.SubRooms.GetRoomSubRooms()
                    .Where(x => x.Purpose == SubRoomPurpose.ScreenDemo && x.IsActive)
                    .Select(x => new ScreenDemoStateEntry(
                        x.Id,
                        x.CreatorId == context.SelfId
                            ? /* инфо о себе */
                            throw new InvalidOperationException()
                            : context.RoomContext.Participants.GetParticipantById(x.CreatorId).ToParticipantInfo(),
                        x.Participants.Select(p => p.Id).ToList()))
                    .ToList();

                return Task.FromResult(HandlerResult.WithResponse(new ScreenDemoStateResponseMessage()
                {
                    Demos = activeDemos,
                }));
            }

            case ScreenDemoStateResponseMessage screenDemoStateResponseMessage:
            {
                var roomId = context.RoomContext.RoomId;

                screenDemoStateService.Clear(roomId);

                foreach (var demo in screenDemoStateResponseMessage.Demos)
                {
                    screenDemoStateService.AddDemo(new ScreenDemoSessionInfo(
                        roomId,
                        demo.SubRoomId,
                        demo.Sharer,
                        demo.ViewerIds));
                }

                return Task.FromResult(HandlerResult.WithEvent(new ScreenDemoStateReceivedEvent()
                {
                    RoomId = roomId,
                    Demos = screenDemoStateResponseMessage.Demos,
                }));
            }

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
