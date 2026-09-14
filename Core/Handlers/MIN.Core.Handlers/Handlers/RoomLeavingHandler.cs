using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.Leaving;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class RoomLeavingHandler : BaseHandler
{
    private readonly IRoomLifecycleManager lifecycle;

    public RoomLeavingHandler(IRoomLifecycleManager lifecycle,
        ILoggerProvider logger) : base(logger)
    {
        this.lifecycle = lifecycle;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.RoomLeave, MessageTypeTag.RoomLeaveAck];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        switch (message)
        {
            case RoomLeaveMessage _:
                lifecycle.MarkParticipantAsLeftRoom(context.RoomContext.RoomId, message.SenderId);
                return Task.FromResult(HandlerResult.WithResponse(new RoomLeaveMessageAckMessage()));

            case RoomLeaveMessageAckMessage _:
                lifecycle.CompleteRoomLeaveAck(context.RoomContext.RoomId);
                return Task.FromResult(HandlerResult.Success(stopPropagation: true));

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
