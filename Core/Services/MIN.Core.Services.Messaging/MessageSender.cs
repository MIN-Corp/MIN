using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Serialization.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Models;
using MIN.Core.Streaming.Contracts.Constants;
using MIN.Core.Streaming.Contracts.Interfaces;
using MIN.Core.Streaming.Contracts.Models;
using MIN.Core.Transport.Contracts.Interfaces;

namespace MIN.Core.Services.Messaging;

/// <inheritdoc cref="IMessageSender"/>
public sealed class MessageSender : IMessageSender, IAsyncDisposable
{
    private readonly ITransport transport;
    private readonly IRoomConnectionRegistry registry;
    private readonly IMessageEncryptor encryptor;
    private readonly IMessageSerializer serializer;
    private readonly IHeaderManager headerManager;
    private readonly IRoomFactory roomFactory;
    private readonly IStreamManager streamManager;

    /// <summary>
    /// Инциализирует новый экземпляр <see cref="MessageSender"/>
    /// </summary>
    public MessageSender(ITransport transport,
        IRoomConnectionRegistry registry,
        IMessageEncryptor encryptor,
        IMessageSerializer serializer,
        IHeaderManager headerManager,
        IRoomFactory roomFactory,
        IStreamManager streamManager)
    {
        this.transport = transport;
        this.registry = registry;
        this.encryptor = encryptor;
        this.serializer = serializer;
        this.headerManager = headerManager;
        this.roomFactory = roomFactory;
        this.streamManager = streamManager;
    }

    /// <inheritdoc />
    public async Task SendAsync(IMessage message, Guid roomId, Guid recipientConnectionId, CancellationToken cancellationToken)
    {
        if (message is IMessageWithSecuredFields messageWithSecured)
        {
            messageWithSecured.Sanitize();
        }

        Guid? serverConnectionId = null;

        if (registry.IsHosting(roomId))
        {
            serverConnectionId = registry.GetServerConnectionIdByRoomId(roomId);
        }

        var serialized = serializer.Serialize(message);

        if (serialized.Length > StreamingConstants.ChunkDataSize)
        {
            var options = new StreamOptions
            {
                IsRawPayload = false,
                RequiresAcks = message.RequireStreamAcks,
                RequiresEncryption = message.RequiresEncryption
            };
            await streamManager.SendAsync(serialized.AsMemory(), options, roomId, recipientConnectionId, serverConnectionId, cancellationToken);
            return;
        }

        var dataWithMarker = headerManager.AddHeader(serialized, (byte)StreamChunkFlags.None);
        var dataToSend = EncryptDataIfRequired(message, dataWithMarker, roomId, recipientConnectionId);

        await transport.SendAsync(dataToSend, recipientConnectionId, serverConnectionId, message.Channel, cancellationToken);
    }

    async Task IMessageSender.SendStreamAsync(Stream messageStream, Guid? streamId, Guid roomId, Guid recipientConnectionId, CancellationToken cancellationToken)
    {
        var options = new StreamOptions
        {
            RequiresAcks = true,
            RequiresEncryption = true,
            StreamId = streamId,
            IsRawPayload = true,
        };

        Guid? serverConnectionId = null;

        if (registry.IsHosting(roomId))
        {
            serverConnectionId = registry.GetServerConnectionIdByRoomId(roomId);
        }

        await streamManager.SendAsync(messageStream, options, roomId, recipientConnectionId, serverConnectionId, cancellationToken);
    }

    async Task IMessageSender.BroadcastAsync(IMessage message, Guid roomId, IEnumerable<Guid>? excludeConnectionIds, CancellationToken cancellationToken)
    {
        if (message is IMessageWithSecuredFields messageWithSecured)
        {
            messageWithSecured.Sanitize();
        }

        var serialized = serializer.Serialize(message);
        var context = roomFactory.GetOrCreateContext(roomId);
        var participants = context.Participants.GetParticipants();

        excludeConnectionIds = (excludeConnectionIds ?? [])
            .Append(CoreRegistryConstants.LocalConnectionId);

        var tasks = participants
            .Select(participant => context.Connections.GetConnectionIdFromParticipantId(participant.Id))
            .Where(connectionId => !excludeConnectionIds.Contains(connectionId))
            .Select(connectionId => SendAsync(message, roomId, connectionId, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private byte[] EncryptDataIfRequired(IMessage message, byte[] plainData, Guid roomId, Guid recipientConnectionId)
    {
        byte[] resultBytes;

        if (message.RequiresEncryption)
        {
            var recipientId = roomFactory.GetOrCreateContext(roomId).Connections.GetParticipantIdFromConnectionId(recipientConnectionId);
            var encrypted = encryptor.EncryptMessage(plainData, recipientId);
            resultBytes = headerManager.AddHeader(encrypted, (byte)HeaderMessageType.Encrypted);
        }
        else
        {
            resultBytes = headerManager.AddHeader(plainData, (byte)HeaderMessageType.Plain);
        }

        return resultBytes;
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
