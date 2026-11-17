using FluentAssertions;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Services;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Services;
using MIN.Core.Services.Contracts.Events;
using MIN.Core.Services.Contracts.Interfaces.Pipeline;
using MIN.Core.Services.Pipeline;
using MIN.Core.Stores.Contracts.Registries.Models;
using MIN.Core.Stores.Registries;
using MIN.Core.Streaming;
using MIN.Core.Streaming.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Events;
using MIN.Tests.Common;
using Moq;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Headers.Contracts.Interfaces;
using Xunit;

namespace MIN.Core.Services.Tests;

/// <summary>
/// Сквозные тесты <see cref="InboundMessagePipeline"/>: провод -> расшифровка -> сборка -> диспетчер
/// </summary>
public class InboundMessagePipelineTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly TestLoggerProvider logger = new();
    private readonly IEventBus eventBus;
    private readonly IRoomConnectionRegistry registry = new RoomConnectionRegistry();
    private readonly CapturingDispatcher dispatcher = new();
    private readonly TestIdentityService identity = new();
    private readonly Mock<IStreamManager> streamManager = new();
    private readonly IHeaderManager headerManager = new HeaderManager();
    private readonly InMemoryTransport assemblerTransport = new();
    private readonly InboundMessagePipeline pipeline;
    private readonly EncryptorPair pair;
    private readonly Guid roomId = Guid.NewGuid();
    private readonly Guid connectionId = Guid.NewGuid();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="InboundMessagePipelineTests"/>
    /// </summary>
    public InboundMessagePipelineTests()
    {
        pair = EncryptorPair.CreateAsync().GetAwaiter().GetResult();
        eventBus = new InMemoryEventBus(logger);
        var assembler = new ChunkBufferAssembler(assemblerTransport, headerManager, logger);
        var ackHandler = new AckHandler(headerManager, streamManager.Object);
        var chunkHandler = new StreamChunkHandler(headerManager, assembler);
        var serializer = TestSerializer.Create();
        var rawHandler = new RawMessageHandler(registry, serializer, dispatcher, pair.Bob, headerManager, rooms.Factory, identity, logger);
        pipeline = new InboundMessagePipeline(assembler, eventBus, rooms.Factory, dispatcher, ackHandler, chunkHandler, rawHandler, identity, logger);
        ((IHostedService)pipeline).StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        registry.RegisterClientConnection(roomId, connectionId);
        rooms.Factory.GetOrCreateContext(roomId).Connections.Register(connectionId, new ParticipantInfo { Id = pair.AliceId });
    }

    private Task DeliverAsync(byte[] wire)
        => eventBus.PublishAsync(new RoomRawMessageReceivedEvent
        {
            EventArgs = new RoomRawMessageReceivedEventArgs(roomId, new RawMessageReceivedEventArgs(wire, connectionId)),
        }, CancellationToken.None);

    /// <summary>
    /// Открытое сообщение по проводу доходит до диспетчера
    /// </summary>
    [Fact]
    public async Task WirePlainMessageShouldReachDispatcher()
    {
        // Arrange
        var message = new MIN.Chat.Messaging.ChatTextMessage { Content = "открытое" };
        var json = TestSerializer.Create().Serialize(message);
        var wire = new byte[] { 0, 0 }.Concat(json).ToArray();

        // Act
        await DeliverAsync(wire);
        await TestWait.UntilAsync(() => dispatcher.Dispatched.Count >= 1);

        // Assert
        ((MIN.Chat.Messaging.ChatTextMessage)dispatcher.Dispatched.Single().Message).Content.Should().Be("открытое");
    }

    /// <summary>
    /// Зашифрованное сообщение по проводу расшифровывается и доходит до диспетчера
    /// </summary>
    [Fact]
    public async Task WireEncryptedMessageShouldReachDispatcher()
    {
        // Arrange
        var message = new MIN.Chat.Messaging.ChatTextMessage { Content = "секретное" };
        var json = TestSerializer.Create().Serialize(message);
        var inner = new byte[] { 0 }.Concat(json).ToArray();
        var wire = new byte[] { 1 }.Concat(pair.Alice.EncryptMessage(inner, pair.BobId)).ToArray();

        // Act
        await DeliverAsync(wire);
        await TestWait.UntilAsync(() => dispatcher.Dispatched.Count >= 1);

        // Assert
        ((MIN.Chat.Messaging.ChatTextMessage)dispatcher.Dispatched.Single().Message).Content.Should().Be("секретное");
    }

    /// <summary>
    /// Одночанковый поток по проводу собирается и диспетчеризуется
    /// </summary>
    [Fact]
    public async Task WireStreamChunkShouldAssembleAndDispatch()
    {
        // Arrange
        var message = new MIN.Chat.Messaging.ChatTextMessage { Content = "чанком" };
        var json = TestSerializer.Create().Serialize(message);
        var header = headerManager.BuildStreamChunkHeader(StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.End, Guid.NewGuid(), 0, 1);
        var inner = header.Concat(json).ToArray();
        var wire = new byte[] { 1 }.Concat(pair.Alice.EncryptMessage(inner, pair.BobId)).ToArray();

        // Act
        await DeliverAsync(wire);
        await TestWait.UntilAsync(() => dispatcher.Dispatched.Count >= 1);

        // Assert
        ((MIN.Chat.Messaging.ChatTextMessage)dispatcher.Dispatched.Single().Message).Content.Should().Be("чанком");
    }

    /// <summary>
    /// ACK по проводу доходит до менеджера потоков
    /// </summary>
    [Fact]
    public async Task WireAckShouldReachStreamManager()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var ack = new byte[21];
        ack[0] = (byte)HeaderMessageType.Ack;
        streamId.TryWriteBytes(ack.AsSpan(1, 16));
        BitConverter.TryWriteBytes(ack.AsSpan(17, 4), 3);

        // Act
        await DeliverAsync(ack);
        await TestWait.UntilAsync(() => streamManager.Invocations.Count >= 1);

        // Assert
        streamManager.Verify(m => m.ProcessAck(It.Is<byte[]>(d => d.SequenceEqual(ack))), Times.Once);
    }

    /// <summary>
    /// Локальное сообщение диспетчеризуется с локальным соединением
    /// </summary>
    [Fact]
    public async Task LocalMessageShouldDispatchWithLocalConnection()
    {
        // Arrange
        var message = new MIN.Chat.Messaging.ChatTextMessage { Content = "локальное" };

        // Act
        await eventBus.PublishAsync(new LocalMessageReceivedEvent(message, roomId, Role.Host), CancellationToken.None);
        await TestWait.UntilAsync(() => dispatcher.Dispatched.Count >= 1);

        // Assert
        var dispatched = dispatcher.Dispatched.Single();
        dispatched.Context.ConnectionId.Should().Be(CoreRegistryConstants.LocalConnectionId);
        dispatched.Context.Role.Should().Be(Role.Host);
    }

    /// <summary>
    /// Битый пакет не роняет конвейер и логируется
    /// </summary>
    [Fact]
    public async Task BrokenPacketShouldNotCrashPipeline()
    {
        // Arrange
        var wire = new byte[] { 1, 2, 3 };

        // Act
        await DeliverAsync(wire);
        await Task.Delay(100);
        var message = new MIN.Chat.Messaging.ChatTextMessage { Content = "после битого" };
        var json = TestSerializer.Create().Serialize(message);
        await DeliverAsync(new byte[] { 0, 0 }.Concat(json).ToArray());
        await TestWait.UntilAsync(() => dispatcher.Dispatched.Count >= 1);

        // Assert
        dispatcher.Dispatched.Should().HaveCount(1);
        logger.HasErrors.Should().BeTrue();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        pipeline.StopAsync().GetAwaiter().GetResult();
        pair.Dispose();
        rooms.Dispose();
    }
}
