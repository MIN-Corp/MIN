using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Handlers.Contracts.Dispatcher;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Headers.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Serialization.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Events;
using MIN.Core.Services.Contracts.Interfaces.Pipeline;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Models;
using MIN.Core.Streaming.Contracts.Events.Receiving;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Services.Pipeline;

/// <summary>
/// Обработчик обычных сообщений: дешифровка, десериализация, диспатч
/// </summary>
public sealed class RawMessageHandler : IRawMessageHandler
{
    private readonly IRoomConnectionRegistry registry;
    private readonly IMessageSerializer serializer;
    private readonly IMessageDispatcher dispatcher;
    private readonly IMessageEncryptor encryptor;
    private readonly IHeaderManager headerManager;
    private readonly IRoomFactory roomFactory;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RawMessageHandler"/>
    /// </summary>
    public RawMessageHandler(IRoomConnectionRegistry registry,
        IMessageSerializer serializer,
        IMessageDispatcher dispatcher,
        IMessageEncryptor encryptor,
        IHeaderManager headerManager,
        IRoomFactory roomFactory,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.registry = registry;
        this.serializer = serializer;
        this.dispatcher = dispatcher;
        this.encryptor = encryptor;
        this.headerManager = headerManager;
        this.roomFactory = roomFactory;
        this.identityService = identityService;
        this.logger = logger;
    }

    ReadOnlyMemory<byte>? IRawMessageHandler.TryDecrypt(RoomRawMessageReceivedEventArgs e)
    {
        if (e.Data.Length == 0)
        {
            return null;
        }

        if (!headerManager.IsEncrypted(e.Data) || e.ConnectionId == CoreRegistryConstants.LocalConnectionId)
        {
            return e.Data.AsMemory(1);
        }

        var context = roomFactory.GetOrCreateContext(e.RoomId);
        if (context.Connections.TryGetParticipantFromConnectionId(e.ConnectionId, out var participantInfo))
        {
            return encryptor.DecryptMessage(e.Data.AsMemory(1), participantInfo.Id);
        }

        logger.Log($"Получили зашифрованное сообщение от неизвестного соединения с id {e.ConnectionId}, игнорю");
        return null;
    }

    async Task IRawMessageHandler.HandleRawAsync(RoomRawMessageReceivedEventArgs e, ReadOnlyMemory<byte> plainData, CancellationToken cancellationToken)
    {
        var context = roomFactory.GetOrCreateContext(e.RoomId);
        var message = serializer.Deserialize(plainData[1..]);
        await dispatcher.DispatchAsync(message, new MessageContext(context, identityService.SelfParticipant.Id,
            e.ConnectionId, registry.GetRole(e.RoomId), cancellationToken));
    }

    async Task IRawMessageHandler.HandleAssembledAsync(MessageAssembledEventArgs e, CancellationToken cancellationToken)
    {
        var context = roomFactory.GetOrCreateContext(e.RoomId);
        var message = serializer.Deserialize(e.Data!);
        await dispatcher.DispatchAsync(message, new MessageContext(context, identityService.SelfParticipant.Id,
            e.ConnectionId, registry.GetRole(e.RoomId), cancellationToken));
    }
}
