using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Voice.Events;
using MIN.Voice.Messaging;
using MIN.Voice.Services.Contacts.Interfaces;

namespace MIN.Voice.Handlers;

internal sealed class VoiceCallEndHandler : BaseHandler
{
    private readonly IEventBus eventBus;
    private readonly IVoiceCallStateService voiceCallStateService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="VoiceCallEndHandler"/>
    /// </summary>
    public VoiceCallEndHandler(IEventBus eventBus,
        IVoiceCallStateService voiceCallStateService,
        ILoggerProvider logger) : base(logger)
    {
        this.eventBus = eventBus;
        this.voiceCallStateService = voiceCallStateService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.VoiceCallEnded];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var voiceCallEndedMessage = (VoiceCallEndedMessage)message;

        var existingVoiceCallStartedMessageId = context.RoomContext.Messages.GetHistory()
            .OfType<VoiceCallStartedMessage>().FirstOrDefault(x => x.SubRoomId == voiceCallEndedMessage.SubRoomId)?.Id;

        if (existingVoiceCallStartedMessageId != null)
        {
            var existing = context.RoomContext.Messages.GetMessageById(existingVoiceCallStartedMessageId.Value) as VoiceCallStartedMessage;
            existing!.EndedAt = DateTime.Now;
            context.RoomContext.Messages.UpdateMessage(existing.Id, existing);
        }

        if (voiceCallStateService.IsInVoiceCall(context.RoomContext.RoomId, voiceCallEndedMessage.SubRoomId))
        {
            await eventBus.PublishAsync(new VoiceCallLeftEvent()
            {
                RoomId = context.RoomContext.RoomId,
                SubRoomId = voiceCallEndedMessage.SubRoomId
            });
        }

        return HandlerResult.WithEvent(new VoiceCallEndedEvent()
        {
            RoomId = context.RoomContext.RoomId,
            SubRoomId = voiceCallEndedMessage.SubRoomId
        });
    }
}
