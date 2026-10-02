using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Voice.Events;
using MIN.Voice.Messaging;

namespace MIN.Voice.Handlers;

internal sealed class VoiceCallStateHandler : BaseHandler
{
    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="VoiceCallStateHandler"/>
    /// </summary>
    public VoiceCallStateHandler(ILoggerProvider logger) : base(logger) { }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.VoiceStateRequest, MessageTypeTag.VoiceStateResponse];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        switch (message)
        {
            case VoiceCallStateRequestMessage _:
                if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var sender))
                {
                    return Task.FromResult(HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true));
                }

                if (context.Role != Role.Host)
                {
                    return Task.FromResult(HandlerResult.Failure($"Получил сообщение {message.GetType()} в {nameof(VoiceCallStateHandler)} как {context.Role}, хотя не должен был", stopPropagation: false));
                }

                var allSubrooms = context.RoomContext.SubRooms.GetRoomSubRooms();
                var voiceCallSubroom = allSubrooms.FirstOrDefault(x => x.Purpose == SubRoomPurpose.Voice && x.IsActive);

                var response = new VoiceCallStateResponseMessage()
                {
                    ActiveSubRoomId = voiceCallSubroom?.Id,
                };

                if (voiceCallSubroom != null)
                {
                    response.StartedAt = voiceCallSubroom.CreatedAt;
                    response.CallParticipantIds = voiceCallSubroom.Participants.Select(x => x.Id).ToList();
                }

                return Task.FromResult(HandlerResult.WithResponse(response));

            case VoiceCallStateResponseMessage voiceCallStateResponseMessage:
                LogInfo($"Получил инфу о текущем звонке: {voiceCallStateResponseMessage.ActiveSubRoomId ?? -1}");

                return Task.FromResult(HandlerResult.WithEvent(new VoiceCallStateReceivedEvent()
                {
                    RoomId = context.RoomContext.RoomId,
                    StartedAt = voiceCallStateResponseMessage.StartedAt,
                    ActiveSubRoomId = voiceCallStateResponseMessage.ActiveSubRoomId,
                    CallParticipants = context.RoomContext.Participants
                        .GetParticipantByIds(voiceCallStateResponseMessage.CallParticipantIds).ToList(),
                }));

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
