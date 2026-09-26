using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.RoomRelated.ParticipantRelated;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class ParticipantLeftHandler : BaseHandler
{
    public ParticipantLeftHandler(ILoggerProvider logger) : base(logger) { }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.ParticipantLeft];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var participantLeftMessage = (ParticipantLeftMessage)message;

        var leavingParticipantId = participantLeftMessage.Participant.Id;

        LogInfo($"Участник {participantLeftMessage.Participant.Name} ({leavingParticipantId}) вышел из комнаты");

        if (participantLeftMessage.IsLeftRoom)
        {
            context.RoomContext.Messages.AddMessage(message);
            context.RoomContext.Participants.RemoveParticipant(leavingParticipantId);
        }
        else
        {
            if (context.RoomContext.Participants.TryGetParticipantById(leavingParticipantId, out var participant))
            {
                participant!.CurrentStatus = OnlineStatus.Offline;
                participant.LastSeenOnline = DateTime.Now;
            }
        }

        return Task.FromResult(HandlerResult.WithEvent(new ParticipantLeftEvent()
        {
            RoomId = context.RoomContext.RoomId,
            Message = participantLeftMessage,
        }));
    }
}
