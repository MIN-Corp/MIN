using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.RoomRelated.ParticipantRelated;
using MIN.Core.Messaging.Stateless.RoomRelated.Join;
using MIN.Core.Messaging.Stateless.RoomRelated.RoomInfo;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class ParticipantJoinHandler : BaseHandler
{
    private readonly IRoomStore roomStore;
    private readonly IIdentityService identityService;
    private readonly IEventBus eventBus;

    public ParticipantJoinHandler(IRoomStore roomStore,
        IIdentityService identityService,
        IEventBus eventBus,
        ILoggerProvider logger) : base(logger)
    {
        this.roomStore = roomStore;
        this.identityService = identityService;
        this.eventBus = eventBus;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.RoomJoinRequest, MessageTypeTag.RoomJoinResponse,
            MessageTypeTag.ParticipantJoined, MessageTypeTag.ParticipantAccepted,
            MessageTypeTag.RoomJoinRejectAck];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        switch (message)
        {
            case RoomJoinRequestMessage roomJoinRequestMessage:
                var room = roomStore.GetRoom(context.RoomContext.RoomId);

                if (room.LocalRoomSettings.PendingKickParticipantIds.Remove(roomJoinRequestMessage.SenderId, out var reason))
                {
                    return HandlerResult.WithErrorHandled(reason, critical: true);
                }

                var isReturning = context.RoomContext.Participants.TryGetParticipantById(roomJoinRequestMessage.SenderId, out var existing)
                    && existing?.CurrentStatus == OnlineStatus.Offline;

                if (!isReturning && room.IsFull)
                {
                    return HandlerResult.WithErrorHandled("Комната заполнена. Попробуйте позже.", critical: true);
                }

                if (!isReturning && existing != null)
                {
                    return HandlerResult.WithErrorHandled("Такой участник в ней уже присутствует. Попробуйте позже.", critical: true);
                }

                return HandlerResult.WithResponse(new RoomJoinResponseMessage());

            case RoomJoinResponseMessage _:
                return HandlerResult.WithResponse(new ParticipantJoinedMessage()
                {
                    Participant = new Participant(identityService.SelfParticipant),
                });

            case ParticipantJoinedMessage participantJoinedMessage:
                LogInfo($"Участник {participantJoinedMessage.Participant.Name} зашёл в комнату с id {context.RoomContext.RoomId}");

                var isRejoin = context.RoomContext.Participants.TryGetParticipantById(participantJoinedMessage.Participant.Id, out var participant);

                if (isRejoin)
                {
                    participant!.CurrentStatus = OnlineStatus.Online;
                }
                else
                {
                    context.RoomContext.Participants.AddParticipant(participantJoinedMessage.Participant);
                    context.RoomContext.Messages.AddMessage(message);
                }

                await eventBus.PublishAsync(new ParticipantJoinedEvent()
                {
                    RoomId = context.RoomContext.RoomId,
                    Message = participantJoinedMessage,
                    IsRejoin = isRejoin,
                }, context.CancellationToken);

                if (context.Role == Role.Host)
                {
                    return HandlerResult.WithResponse(new ParticipantAcceptedMessage());
                }

                return HandlerResult.Success();

            case ParticipantAcceptedMessage _:
                return HandlerResult.WithResponse(new RoomInfoRequestMessage()
                {
                    IsRejoin = context.RoomContext.Messages.GetFirstMessage() != null
                });

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
