using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.RoomRelated;
using MIN.Core.Messaging.Stateless.FastChannelConnect;
using MIN.Core.Messaging.Stateless.Handshake;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class HandshakeHandler : BaseHandler
{
    private readonly IMessageEncryptor encryptor;
    private readonly IRoomStore roomStore;
    private readonly IIdentityService identityService;
    private readonly IVersionProvider versionProvider;

    public HandshakeHandler(IMessageEncryptor encryptor,
        IRoomStore roomStore,
        IIdentityService identityService,
        IVersionProvider versionProvider,
        ILoggerProvider logger) : base(logger)
    {
        this.encryptor = encryptor;
        this.roomStore = roomStore;
        this.identityService = identityService;
        this.versionProvider = versionProvider;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.Handshake, MessageTypeTag.HandshakeAck];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
        => message switch
        {
            HandshakeMessage handshakeMessage => await HandleHandshake(handshakeMessage, context),
            HandshakeAckMessage handshakeAckMessage => await HandleHandshakeAck(handshakeAckMessage, context),
            PublicKeyRequestMessage => await HandleKeyRequest(context),
            PublicKeyResponseMessage keyResponse => await HandleKeyResponse(keyResponse, context),
            _ => throw new HandlerTypeMismatch(this, message),
        };

    private async Task<HandlerResult> HandleHandshake(HandshakeMessage handshakeMessage, MessageContext context)
    {
        if (!context.RoomContext.Connections.TryRegister(context.ConnectionId, handshakeMessage.Participant))
        {
            return HandlerResult.WithErrorHandled("Произошла коллизия идентификаторов соединения. Попробуйте ещё раз.",
                critical: true);
        }

        if (!versionProvider.IsVersionCompatible(handshakeMessage.Version))
        {
            var selfVersion = versionProvider.Version;
            var clientOnOlderVersion = selfVersion > handshakeMessage.Version ? "Вы" : "Хост";
            return HandlerResult.WithErrorHandled($"{clientOnOlderVersion} на устаревшей версии: \nВаша версия - {handshakeMessage.Version}\nВерсия хоста комнаты - {selfVersion}",
                critical: true);
        }

        var participantId = handshakeMessage.Participant.Id;
        var keyNegotiation = await NegotiateKeyAsync(participantId,
            handshakeMessage.PublicKey, handshakeMessage.PublicKeyFingerprint, handshakeMessage.Participant.Name);

        if (keyNegotiation.NeedFullKey)
        {
            return HandlerResult.WithResponse(new PublicKeyRequestMessage());
        }

        var localKey = await encryptor.GetLocalPublicKey();
        var localFingerprint = encryptor.ComputeKeyFingerprint(localKey);

        LogInfo($"Сессия с отправителем {handshakeMessage.Participant.Name} инициализирована");

        return HandlerResult.WithResponse(new HandshakeAckMessage()
        {
            Participant = identityService.SelfParticipant.ToParticipantInfo(),
            PublicKey = keyNegotiation.SendFullKey ? localKey : null,
            PublicKeyFingerprint = keyNegotiation.SendFullKey ? null : localFingerprint,
        });
    }

    private async Task<HandlerResult> HandleHandshakeAck(HandshakeAckMessage handshakeAckMessage, MessageContext context)
    {
        if (!context.RoomContext.Connections.TryRegister(context.ConnectionId, handshakeAckMessage.Participant))
        {
            return HandlerResult.Failure("Произошла коллизия идентификаторов соединения с хостом. Попробуйте ещё раз.");
        }

        var hostId = handshakeAckMessage.Participant.Id;
        var keyNegotiation = await NegotiateKeyAsync(hostId,
            handshakeAckMessage.PublicKey, handshakeAckMessage.PublicKeyFingerprint, handshakeAckMessage.Participant.Name);

        if (keyNegotiation.NeedFullKey)
        {
            return HandlerResult.WithResponse(new PublicKeyRequestMessage());
        }

        LogInfo($"Сессия с получателем {handshakeAckMessage.Participant.Name} инициализирована");

        return await BuildFastChannelResponse(context);
    }

    private async Task<HandlerResult> HandleKeyRequest(MessageContext context)
    {
        return HandlerResult.WithResponse(new PublicKeyResponseMessage()
        {
            SenderId = identityService.SelfParticipant.Id,
            PublicKey = await encryptor.GetLocalPublicKey(),
        });
    }

    private async Task<HandlerResult> HandleKeyResponse(PublicKeyResponseMessage keyResponse, MessageContext context)
    {
        var partnerId = keyResponse.SenderId;

        // если ключ уже был сохранён — это смена ключа, а не первый контакт
        var hadStoredKey = await encryptor.TryGetPartnerKeyFingerprintAsync(partnerId) != null;

        await encryptor.InitializeSessionWithPartnerAsync(partnerId, keyResponse.PublicKey);

        if (hadStoredKey)
        {
            var warning = new SystemTextMessage()
            {
                Content = $"Участник сменил ключ шифрования. Соединение защищено новым ключом.",
            };

            context.RoomContext.Messages.AddMessage(warning);
            LogWarning($"Партнёр {partnerId} сменил публичный ключ");
        }
        else
        {
            LogInfo($"Получен публичный ключ партнёра {partnerId}");
        }

        if (context.Role == Role.Host)
        {
            var localKey = await encryptor.GetLocalPublicKey();
            var localFingerprint = encryptor.ComputeKeyFingerprint(localKey);

            return HandlerResult.WithResponse(new HandshakeAckMessage()
            {
                Participant = identityService.SelfParticipant.ToParticipantInfo(),
                PublicKey = null,
                PublicKeyFingerprint = localFingerprint,
            });
        }

        return await BuildFastChannelResponse(context);
    }

    /// <summary>
    /// Согласование ключа по отпечатку: полный ключ, инициализация из сохранённого или запрос ключа
    /// </summary>
    private async Task<KeyNegotiationResult> NegotiateKeyAsync(Guid partnerId,
        byte[]? publicKey, byte[]? keyFingerprint, string partnerName)
    {
        // первый контакт: полный ключ передан явно
        if (publicKey != null)
        {
            await encryptor.InitializeSessionWithPartnerAsync(partnerId, publicKey);
            return new KeyNegotiationResult { SendFullKey = true };
        }

        var storedFingerprint = await encryptor.TryGetPartnerKeyFingerprintAsync(partnerId);

        // совпадение отпечатков: инициализируем из сохранённого ключа, ничего не перезаписываем
        if (keyFingerprint != null
            && storedFingerprint != null
            && keyFingerprint.SequenceEqual(storedFingerprint))
        {
            if (await encryptor.TryInitializeSessionFromStoredAsync(partnerId))
            {
                return new KeyNegotiationResult();
            }
        }
        else if (storedFingerprint != null)
        {
            LogWarning($"Партнёр {partnerName} сменил публичный ключ");
        }

        // ключ неизвестен, отпечатки расходятся или сохранённый ключ повреждён
        return new KeyNegotiationResult { NeedFullKey = true };
    }

    private async Task<HandlerResult> BuildFastChannelResponse(MessageContext context)
    {
        var savedRoom = roomStore.GetRoom(context.RoomContext.RoomId);

        if (savedRoom == null || !savedRoom.ConnectionAddresses.Any())
        {
            return HandlerResult.Failure("Не нашлась комната. Попробуйте ещё раз.");
        }

        return HandlerResult.WithResponse(new FastChannelConnectRequestMessage()
        {
            AddressOrigin = savedRoom.ConnectionAddresses.First().Origin
        });
    }

    private sealed class KeyNegotiationResult
    {
        public bool NeedFullKey { get; init; }
        public bool SendFullKey { get; init; }
    }
}
