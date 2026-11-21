using FluentAssertions;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Services;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Services;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Discovery.Events;
using MIN.Discovery.Messaging;
using MIN.Discovery.Services;
using MIN.Discovery.Services.Contracts.Interfaces;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Discovery.Tests;

/// <summary>
/// Тесты на <see cref="UdpDiscoveryService"/>
/// </summary>
public class UdpDiscoveryServiceTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly TestLoggerProvider logger = new();
    private readonly InMemoryEventBus eventBus;
    private readonly IRoomStore roomStore;
    private readonly FakeDiscoveryTransport transport = new();
    private readonly IDiscoveryService service;
    private readonly TcpEndpoint lanEndpoint = new() { IpAddress = "192.168.1.10", Port = 5000, Origin = AddressOrigin.Lan };

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="UdpDiscoveryServiceTests"/>
    /// </summary>
    public UdpDiscoveryServiceTests()
    {
        eventBus = new InMemoryEventBus(logger);
        roomStore = new RoomStore(rooms.Factory);
        service = new UdpDiscoveryService(transport, TestSerializer.Create(), roomStore, eventBus, logger);
    }

    private Room CreateRoom(string name = "Комната")
        => new()
        {
            Name = name,
            HostParticipant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Хост" },
            ConnectionAddresses = [lanEndpoint],
        };

    /// <summary>
    /// Объявленная комната отвечает на запрос обнаружения
    /// </summary>
    [Fact]
    public async Task StartDiscoveryShouldAnnounceRoom()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var responder = new CapturingResponder();

        // Act
        await service.StartDiscoveryAsync(new RoomInfo(room), [lanEndpoint], CancellationToken.None);
        transport.Deliver(TestSerializer.Create().Serialize(new DiscoveryRequestMessage()), responder);
        await TestWait.UntilAsync(() => responder.Responses.Count >= 1);

        // Assert
        transport.StartCalls.Should().Be(1);
        var response = (DiscoveryResponseMessage)TestSerializer.Create().Deserialize(responder.Responses.Single());
        response.RoomDiscoveryInfos.Should().ContainSingle();
        response.RoomDiscoveryInfos[0].Room.Id.Should().Be(room.Id);
        response.RoomDiscoveryInfos[0].Room.Name.Should().Be("Комната");
    }

    /// <summary>
    /// Без LAN TCP-адреса объявление отклоняется
    /// </summary>
    [Fact]
    public async Task StartDiscoveryWithoutLanTcpShouldThrow()
    {
        // Arrange
        var room = CreateRoom();

        // Act
        var act = () => service.StartDiscoveryAsync(new RoomInfo(room), [], CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OverflowException>();
    }

    /// <summary>
    /// Слишком большая комната не влезает в лимит датаграммы
    /// </summary>
    [Fact]
    public async Task OversizedRoomShouldThrow()
    {
        // Arrange
        var room = CreateRoom(new string('я', 1600));

        // Act
        var act = () => service.StartDiscoveryAsync(new RoomInfo(room), [lanEndpoint], CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OverflowException>();
    }

    /// <summary>
    /// Суммарный размер комнат ограничивает объявление
    /// </summary>
    [Fact]
    public async Task TotalSizeLimitShouldThrow()
    {
        // Arrange
        var first = CreateRoom(new string('a', 500));
        var second = CreateRoom(new string('b', 500));
        await service.StartDiscoveryAsync(new RoomInfo(first), [lanEndpoint], CancellationToken.None);

        // Act
        var act = () => service.StartDiscoveryAsync(new RoomInfo(second), [lanEndpoint], CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OverflowException>();
    }

    /// <summary>
    /// Обнаружение публикует событие с найденными комнатами
    /// </summary>
    [Fact]
    public async Task DiscoverShouldPublishRoomDiscoveredEvent()
    {
        // Arrange
        using var capture = new EventCapture<RoomDiscoveredEvent>(eventBus);
        var roomInfo = new RoomInfo { Name = "Чужая комната" };
        var response = new DiscoveryResponseMessage
        {
            RoomDiscoveryInfos = [new Services.Contracts.Models.RoomDiscoveryInfo { Room = roomInfo, Endpoints = [lanEndpoint] }],
        };
        var responseData = TestSerializer.Create().Serialize(response);
        transport.BroadcastHandler = (_, _) =>
        {
            transport.Deliver(responseData, new CapturingResponder());
            return Task.CompletedTask;
        };

        // Act
        await service.DiscoverRoomsAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None);
        await capture.WaitAsync();

        // Assert
        transport.Broadcasts.Should().HaveCount(1);
        capture.Events.Single().RoomDiscoveryInfos.Should().ContainSingle().Which.Room.Id.Should().Be(roomInfo.Id);
    }

    /// <summary>
    /// Повторные ответы о той же комнате дедуплицируются
    /// </summary>
    [Fact]
    public async Task DiscoverShouldDeduplicateRooms()
    {
        // Arrange
        using var capture = new EventCapture<RoomDiscoveredEvent>(eventBus);
        var roomInfo = new RoomInfo { Name = "Комната" };
        var response = new DiscoveryResponseMessage
        {
            RoomDiscoveryInfos = [new Services.Contracts.Models.RoomDiscoveryInfo { Room = roomInfo, Endpoints = [lanEndpoint] }],
        };
        var responseData = TestSerializer.Create().Serialize(response);
        transport.BroadcastHandler = (_, _) =>
        {
            transport.Deliver(responseData, new CapturingResponder());
            transport.Deliver(responseData, new CapturingResponder());
            return Task.CompletedTask;
        };

        // Act
        await service.DiscoverRoomsAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None);
        await capture.WaitAsync();
        await Task.Delay(100);

        // Assert
        capture.Events.Should().HaveCount(1);
        capture.Events.Single().RoomDiscoveryInfos.Should().HaveCount(1);
    }

    /// <summary>
    /// Новый поиск сбрасывает дедупликацию
    /// </summary>
    [Fact]
    public async Task DiscoverAgainShouldResetDedup()
    {
        // Arrange
        using var capture = new EventCapture<RoomDiscoveredEvent>(eventBus);
        var roomInfo = new RoomInfo { Name = "Комната" };
        var response = new DiscoveryResponseMessage
        {
            RoomDiscoveryInfos = [new Services.Contracts.Models.RoomDiscoveryInfo { Room = roomInfo, Endpoints = [lanEndpoint] }],
        };
        var responseData = TestSerializer.Create().Serialize(response);
        transport.BroadcastHandler = (_, _) =>
        {
            transport.Deliver(responseData, new CapturingResponder());
            return Task.CompletedTask;
        };

        // Act
        await service.DiscoverRoomsAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        await capture.WaitAsync();
        await service.DiscoverRoomsAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        await capture.WaitAsync(2);

        // Assert
        capture.Events.Should().HaveCount(2);
    }

    /// <summary>
    /// Остановка последней комнаты выключает прослушивание
    /// </summary>
    [Fact]
    public async Task StopDiscoveryShouldStopListeningWhenLast()
    {
        // Arrange
        var room = CreateRoom();
        await service.StartDiscoveryAsync(new RoomInfo(room), [lanEndpoint], CancellationToken.None);

        // Act
        await service.StopDiscoveryAsync(room.Id);

        // Assert
        transport.StopCalls.Should().Be(1);
    }

    /// <summary>
    /// Запрос по незарегистрированной комнате даёт пустой ответ
    /// </summary>
    [Fact]
    public async Task RequestForUnknownRoomShouldRespondEmpty()
    {
        // Arrange
        var room = CreateRoom();
        var responder = new CapturingResponder();
        await service.StartDiscoveryAsync(new RoomInfo(room), [lanEndpoint], CancellationToken.None);

        // Act
        transport.Deliver(TestSerializer.Create().Serialize(new DiscoveryRequestMessage()), responder);
        await TestWait.UntilAsync(() => responder.Responses.Count >= 1);

        // Assert
        var response = (DiscoveryResponseMessage)TestSerializer.Create().Deserialize(responder.Responses.Single());
        response.RoomDiscoveryInfos.Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ((IAsyncDisposable)service).DisposeAsync().AsTask().GetAwaiter().GetResult();
        rooms.Dispose();
    }
}
