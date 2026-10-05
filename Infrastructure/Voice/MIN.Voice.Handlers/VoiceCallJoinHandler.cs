using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Voice.Events;
using MIN.Voice.Messaging;
using MIN.Voice.Services.Contacts.Interfaces;

namespace MIN.Voice.Handlers;

internal sealed class VoiceCallJoinHandler : BaseHandler
{
    private readonly IEventBus eventBus;
    private readonly IMessageRouter messageRouter;
    private readonly IVoicePlaybackService voicePlaybackService;
    private readonly IIdentityService identityService;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="VoiceCallJoinHandler"/>
    /// </summary>
    public VoiceCallJoinHandler(IEventBus eventBus,
        IMessageRouter messageRouter,
        IVoicePlaybackService voicePlaybackService,
        IIdentityService identityService,
        ILoggerProvider logger) : base(logger)
    {
        this.eventBus = eventBus;
        this.messageRouter = messageRouter;
        this.voicePlaybackService = voicePlaybackService;
        this.identityService = identityService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes =>
        [MessageTypeTag.VoiceCallJoinRequest, MessageTypeTag.VoiceCallEstablished];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        switch (message)
        {
            case VoiceCallJoinRequestMessage voiceCallJoinRequestMessage:
                if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var sender))
                {
                    return HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true);
                }

                if (context.Role != Role.Host)
                {
                    return HandlerResult.Failure($"Получил сообщение {message.GetType()} в {nameof(VoiceCallJoinHandler)} как {context.Role}, хотя не должен был", stopPropagation: false);
                }

                var subRoomInfo = context.RoomContext.SubRooms.GetSubRoom(voiceCallJoinRequestMessage.SubRoomId);

                if (subRoomInfo == null)
                {
                    return HandlerResult.WithErrorHandled("Такая подкомната не нашлась");
                }

                var senderParicipantInfo = sender!.ToParticipantInfo();

                var joinResult = context.RoomContext.SubRooms.TryJoinSubRoom(voiceCallJoinRequestMessage.SubRoomId, senderParicipantInfo);

                if (joinResult != SubRoomJoinOutcome.Success)
                {
                    var error = joinResult switch
                    {
                        SubRoomJoinOutcome.RoomNotFound => "Комната не нашлась",
                        SubRoomJoinOutcome.SubRoomNotFound => "Нету информации о подкомнате",
                        SubRoomJoinOutcome.AlreadyJoined => "Вы уже учавствуете в звонке",
                        _ => "Не удалось войти"
                    };

                    return HandlerResult.WithErrorHandled(error);
                }

                return HandlerResult.WithResponse(new VoiceCallEstablishedMessage()
                {
                    NeedToAnnounce = true,
                    SubRoomId = subRoomInfo.Id,
                    CallParticipantIds = subRoomInfo.Participants
                        .Where(x => x.Id != senderParicipantInfo.Id).Select(x => x.Id).ToList()
                });

            case VoiceCallEstablishedMessage voiceCallEstablishedMessage:
                foreach (var participantId in voiceCallEstablishedMessage.CallParticipantIds)
                {
                    voicePlaybackService.AddParticipant(participantId);
                }

                await eventBus.PublishAsync(new VoiceCallEstablishedEvent()
                {
                    RoomId = context.RoomContext.RoomId,
                    SubRoomId = voiceCallEstablishedMessage.SubRoomId,
                });

                if (!voiceCallEstablishedMessage.NeedToAnnounce)
                {
                    return HandlerResult.Success();
                }

                var selfParticipant = identityService.SelfParticipant.ToParticipantInfo();

                var voiceParticipantJoinedMessage = new VoiceParticipantJoinedMessage()
                {
                    SubRoomId = voiceCallEstablishedMessage.SubRoomId,
                    Participant = selfParticipant,
                };

                await messageRouter.RouteAsync(voiceParticipantJoinedMessage, context.RoomContext.RoomId, selfParticipant.Id, context.CancellationToken);

                return HandlerResult.Success();

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
