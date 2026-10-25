using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionParticipantLeftHandler : BaseHandler
{
    private readonly ISessionInstanceManager sessionInstanceManager;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="SessionParticipantLeftHandler"/>
    /// </summary>
    public SessionParticipantLeftHandler(ISessionInstanceManager sessionInstanceManager,
        ILoggerProvider logger) : base(logger)
    {
        this.sessionInstanceManager = sessionInstanceManager;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.SessionParticipantLeft];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var sessionParticipantLeftMessage = (SessionParticipantLeftMessage)message;
        var roomId = context.RoomContext.RoomId;
        var subRoomId = sessionParticipantLeftMessage.SubRoomId;
        var participant = sessionParticipantLeftMessage.Participant;

        if (!sessionParticipantLeftMessage.IsLast)
        {
            var sessionInstances = sessionInstanceManager.GetInstances(roomId, subRoomId);

            foreach (var sessionInstance in sessionInstances)
            {
                sessionInstance.NotifyParticipantLeft(participant.Id);
            }
        }

        var existingSessionReadyMessageId = context.RoomContext.Messages.GetHistory()
            .OfType<SessionReadyMessage>().FirstOrDefault(x => x.SubRoomId == sessionParticipantLeftMessage.SubRoomId)?.Id;

        if (existingSessionReadyMessageId != null)
        {
            var existing = context.RoomContext.Messages.GetMessageById(existingSessionReadyMessageId.Value) as SessionReadyMessage;
            existing!.CurrentParticipantAmount--;
            context.RoomContext.Messages.UpdateMessage(existing.Id, existing);
        }

        return Task.FromResult(HandlerResult.WithEvent(new SessionParticipantLeftEvent()
        {
            Participant = participant,
            SubRoomId = subRoomId,
            RoomId = roomId,
        }));
    }
}
