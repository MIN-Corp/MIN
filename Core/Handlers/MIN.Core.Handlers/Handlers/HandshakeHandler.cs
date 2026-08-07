using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Handlers.Contracts;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.FastChannelConnect;
using MIN.Core.Messaging.Stateless.Handshake;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class HandshakeHandler : IMessageHandler
{
    private readonly IMessageEncryptor encryptor;
    private readonly INetworkErrorHandler networkErrorHandler;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;
    private readonly IVersionProvider versionProvider;

    public HandshakeHandler(IMessageEncryptor encryptor,
        INetworkErrorHandler networkErrorHandler,
        IIdentityService identityService,
        IVersionProvider versionProvider,
        ILoggerProvider logger)
    {
        this.encryptor = encryptor;
        this.networkErrorHandler = networkErrorHandler;
        this.identityService = identityService;
        this.versionProvider = versionProvider;
        this.logger = logger;
    }

    IEnumerable<MessageTypeTag> IMessageHandler.HandledTypes
        => [MessageTypeTag.Handshake, MessageTypeTag.HandshakeAck];

    int IMessageHandler.Priority => 0;

    async Task<HandlerResult> IMessageHandler.HandleAsync(IMessage message, MessageContext context)
    {
        if (message is HandshakeMessage handshakeMessage)
        {
            context.RoomContext.Connections.Register(context.ConnectionId, handshakeMessage.Participant);

            var selfVersion = versionProvider.Version;

            if (!versionProvider.IsVersionCompatible(handshakeMessage.Version))
            {
                var clientOnOlderVersion = selfVersion > handshakeMessage.Version ? "Вы" : "Хост";
                await networkErrorHandler.SendErrorAsync(
                    $"{clientOnOlderVersion} на устаревшей версии: \nВаша версия - {handshakeMessage.Version}\nВерсия хоста комнаты - {selfVersion}",
                    handshakeMessage.Participant.Id,
                    context.RoomContext.RoomId,
                    critical: true);
                return HandlerResult.Success();
            }

            await encryptor.InitializeSessionWithPartnerAsync(handshakeMessage.Participant.Id, handshakeMessage.PublicKey);
            logger.Log($"Сессия с отправителем {handshakeMessage.Participant.Name} инициализирована");

            return HandlerResult.WithResponse(new HandshakeAckMessage()
            {
                Participant = identityService.SelfParticipant.ToParticipantInfo(),
                PublicKey = await encryptor.GetLocalPublicKey(),
            });
        }
        else if (message is HandshakeAckMessage handshakeAckMessage)
        {
            await encryptor.InitializeSessionWithPartnerAsync(handshakeAckMessage.Participant.Id, handshakeAckMessage.PublicKey);
            context.RoomContext.Connections.Register(context.ConnectionId, handshakeAckMessage.Participant);

            logger.Log($"Сессия с получателем {handshakeAckMessage.Participant.Name} инициализирована");

            return HandlerResult.WithResponse(new FastChannelConnectRequestMessage());
        }

        return HandlerResult.Failure($"Неизвестный тип сообщения в {nameof(HandshakeHandler)} - {message.GetType()}");
    }
}
