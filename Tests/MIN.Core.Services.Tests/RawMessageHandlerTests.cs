using FluentAssertions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Headers.Services;
using MIN.Core.Services.Contracts.Events;
using MIN.Core.Services.Contracts.Interfaces.Pipeline;
using MIN.Core.Services.Pipeline;
using MIN.Core.Stores.Registries;
using MIN.Core.Transport.Contracts.Events;
using MIN.Tests.Common;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using Xunit;

namespace MIN.Core.Services.Tests;

/// <summary>
/// Тесты на <see cref="RawMessageHandler"/>
/// </summary>
public class RawMessageHandlerTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly TestLoggerProvider logger = new();
    private readonly IRoomConnectionRegistry registry = new RoomConnectionRegistry();
    private readonly CapturingDispatcher dispatcher = new();
    private readonly TestIdentityService identity = new();
    private readonly Guid roomId = Guid.NewGuid();
    private readonly Guid connectionId = Guid.NewGuid();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RawMessageHandlerTests"/>
    /// </summary>
    public RawMessageHandlerTests()
    {
        registry.RegisterClientConnection(roomId, connectionId);
    }

    private RawMessageHandler CreateHandler(MIN.Core.Cryptography.Contracts.Interfaces.IMessageEncryptor encryptor)
        => new(registry, TestSerializer.Create(), dispatcher, encryptor, new HeaderManager(), rooms.Factory, identity, logger);

    private RoomRawMessageReceivedEventArgs Args(byte[] data)
        => new(roomId, new RawMessageReceivedEventArgs(data, connectionId));

    /// <summary>
    /// Незашифрованные данные теряют только внешний маркер
    /// </summary>
    [Fact]
    public async Task TryDecryptPlainShouldStripOuterHeader()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        IRawMessageHandler handler = CreateHandler(pair.Bob);
        var inner = new byte[] { 0, 1, 2, 3 };
        var wire = new byte[] { 0 }.Concat(inner).ToArray();

        // Act
        var plain = handler.TryDecrypt(Args(wire));

        // Assert
        plain.Should().NotBeNull();
        plain!.Value.ToArray().Should().Equal(inner);
    }

    /// <summary>
    /// Пустой пакет игнорируется, а не роняет обработчик
    /// </summary>
    [Fact]
    public async Task TryDecryptEmptyDataShouldReturnNull()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        IRawMessageHandler handler = CreateHandler(pair.Bob);

        // Act
        var plain = handler.TryDecrypt(Args([]));

        // Assert
        plain.Should().BeNull();
    }

    /// <summary>
    /// Зашифрованные данные от известного соединения расшифровываются
    /// </summary>
    [Fact]
    public async Task TryDecryptEncryptedShouldDecrypt()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        rooms.Factory.GetOrCreateContext(roomId).Connections.Register(connectionId, new ParticipantInfo { Id = pair.AliceId });
        IRawMessageHandler handler = CreateHandler(pair.Bob);
        var inner = new byte[] { 0, 9, 8, 7 };
        var encrypted = pair.Alice.EncryptMessage(inner, pair.BobId);
        var wire = new byte[] { 1 }.Concat(encrypted).ToArray();

        // Act
        var plain = handler.TryDecrypt(Args(wire));

        // Assert
        plain!.Value.ToArray().Should().Equal(inner);
    }

    /// <summary>
    /// Зашифрованные данные от неизвестного соединения игнорируются
    /// </summary>
    [Fact]
    public async Task TryDecryptEncryptedUnknownConnectionShouldReturnNull()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        IRawMessageHandler handler = CreateHandler(pair.Bob);
        var encrypted = pair.Alice.EncryptMessage([0, 1], pair.BobId);
        var wire = new byte[] { 1 }.Concat(encrypted).ToArray();

        // Act
        var plain = handler.TryDecrypt(Args(wire));

        // Assert
        plain.Should().BeNull();
    }

    /// <summary>
    /// Локальное соединение не расшифровывается
    /// </summary>
    [Fact]
    public async Task TryDecryptLocalConnectionShouldSkipDecryption()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        IRawMessageHandler handler = CreateHandler(pair.Bob);
        var wire = new byte[] { 1, 2, 3 };
        var localArgs = new RoomRawMessageReceivedEventArgs(roomId, new RawMessageReceivedEventArgs(wire, Stores.Contracts.Registries.Models.CoreRegistryConstants.LocalConnectionId));

        // Act
        var plain = handler.TryDecrypt(localArgs);

        // Assert
        plain!.Value.ToArray().Should().Equal([2, 3]);
    }

    /// <summary>
    /// Сырое сообщение десериализуется и диспетчеризуется
    /// </summary>
    [Fact]
    public async Task HandleRawAsyncShouldDispatchDeserializedMessage()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        IRawMessageHandler handler = CreateHandler(pair.Bob);
        var message = new MIN.Chat.Messaging.ChatTextMessage { Content = "по проводу", SenderId = Guid.NewGuid() };
        var json = TestSerializer.Create().Serialize(message);
        var plainData = new byte[] { 0 }.Concat(json).ToArray();

        // Act
        await handler.HandleRawAsync(Args([0]), plainData, CancellationToken.None);

        // Assert
        dispatcher.Dispatched.Should().HaveCount(1);
        var dispatched = dispatcher.Dispatched.Single();
        dispatched.Message.Should().BeOfType<MIN.Chat.Messaging.ChatTextMessage>();
        ((MIN.Chat.Messaging.ChatTextMessage)dispatched.Message).Content.Should().Be("по проводу");
        dispatched.Context.ConnectionId.Should().Be(connectionId);
        dispatched.Context.SelfId.Should().Be(identity.SelfParticipant.Id);
    }

    /// <summary>
    /// Собранное с потока сообщение десериализуется и диспетчеризуется
    /// </summary>
    [Fact]
    public async Task HandleAssembledAsyncShouldDispatch()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        IRawMessageHandler handler = CreateHandler(pair.Bob);
        var message = new MIN.Chat.Messaging.ChatTextMessage { Content = "из потока" };
        var json = TestSerializer.Create().Serialize(message);
        var assembled = new MIN.Core.Streaming.Contracts.Events.Receiving.MessageAssembledEventArgs
        {
            StreamId = Guid.NewGuid(),
            RoomId = roomId,
            ConnectionId = connectionId,
            Data = json,
        };

        // Act
        await handler.HandleAssembledAsync(assembled, CancellationToken.None);

        // Assert
        dispatcher.Dispatched.Should().HaveCount(1);
        ((MIN.Chat.Messaging.ChatTextMessage)dispatcher.Dispatched.Single().Message).Content.Should().Be("из потока");
    }

    /// <inheritdoc />
    public void Dispose() => rooms.Dispose();
}
