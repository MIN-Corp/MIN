using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.ParticipantRelated;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class ParticipantUpdateHandler : BaseHandler
{
    public ParticipantUpdateHandler(ILoggerProvider logger) : base(logger) { }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.ParticipantUpdated];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var updatedParticipant = (ParticipantUpdatedMessage)message;

        if (context.Role == Role.Host && message.SenderId != updatedParticipant.NewParticipant.Id)
        {
            return Task.FromResult(HandlerResult.Success(stopPropagation: true));
        }

        if (context.RoomContext.Participants.TryGetParticipantById(updatedParticipant.NewParticipant.Id, out var existing)
            && existing?.Name == updatedParticipant.NewParticipant.Name)
        {
            return Task.FromResult(HandlerResult.Success());
        }

        context.RoomContext.Participants.UpdateParticipant(updatedParticipant.NewParticipant.Id, updatedParticipant.NewParticipant);
        return Task.FromResult(HandlerResult.WithEvent(new ParticipantUpdatedEvent()
        {
            RoomId = context.RoomContext.RoomId,
            NewParticipant = updatedParticipant.NewParticipant
        }));
    }
}
