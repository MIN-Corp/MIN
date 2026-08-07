using System.Text.Json;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Serialization.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Discovery.Events;
using MIN.Discovery.Messaging;
using MIN.Discovery.Services.Contracts.Enums;
using MIN.Discovery.Services.Contracts.Interfaces;
using MIN.Discovery.Services.Contracts.Models;
using MIN.Discovery.Transport.Contracts;
using MIN.Discovery.Transport.Contracts.Events;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Discovery.Services;

/// <summary>
/// <inheritdoc cref="IDiscoveryService"/> на базе UDP Broadcast
/// </summary>
public sealed class UdpDiscoveryService : IDiscoveryService, IAsyncDisposable
{
    private readonly IDiscoveryTransport discoveryTransport;
    private readonly IMessageSerializer serializer;
    private readonly IRoomStore roomStore;
    private readonly IEventBus eventBus;
    private readonly ILoggerProvider logger;
    private readonly HashSet<Guid> activeRoomIds = [];
    private readonly HashSet<Guid> discoveredRoomIds = [];
    private readonly CancellationTokenSource serviceCts;
    private readonly Dictionary<Guid, int> activeRoomsSizeById = [];

    DiscoveryMethod IDiscoveryService.DiscoveryMethod => DiscoveryMethod.Local;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="UdpDiscoveryService"/>
    /// </summary>
    public UdpDiscoveryService(IDiscoveryTransport discoveryTransport,
        IMessageSerializer serializer,
        IRoomStore roomStore,
        IEventBus eventBus,
        ILoggerProvider logger)
    {
        this.discoveryTransport = discoveryTransport;
        this.serializer = serializer;
        this.roomStore = roomStore;
        this.eventBus = eventBus;
        this.logger = logger;

        serviceCts = new CancellationTokenSource();
        discoveryTransport.MessageReceived += OnRequestReceived;
    }

    async Task IDiscoveryService.StartDiscoveryAsync(RoomInfo room, IEnumerable<IEndpoint> endpoints, CancellationToken cancellationToken)
    {
        var roomId = room.Id;

        var lan = endpoints.FirstOrDefault(x => x.Origin == AddressOrigin.LAN && x.Type == TransportType.Tcp)
            ?? throw new OverflowException("должен быть указан LAN для локального обнаружения");

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new DiscoveryResponseMessage()
            {
                RoomDiscoveryInfos = [new RoomDiscoveryInfo()
                {
                    Room = room,
                    Endpoints = [lan]
                }]
            });

        if (activeRoomsSizeById.Sum(x => x.Value) + bytes.Length >= 1500)
        {
            throw new OverflowException("достигнут лимита комнат по локальному обнаружению");
        }

        activeRoomsSizeById[roomId] = bytes.Length;
        activeRoomIds.Add(roomId);
        await discoveryTransport.StartListeningAsync(serviceCts.Token);
    }

    /// <inheritdoc />
    public async Task StopDiscoveryAsync(Guid roomId)
    {
        activeRoomsSizeById.Remove(roomId);
        activeRoomIds.Remove(roomId);

        if (activeRoomIds.Count == 0)
        {
            await discoveryTransport.StopListeningAsync();
        }
    }

    async Task IDiscoveryService.DiscoverRoomsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        discoveredRoomIds.Clear();

        var request = new DiscoveryRequestMessage();
        var requestData = serializer.Serialize(request);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);
        discoveryTransport.MessageReceived += OnResponseReceived;

        try
        {
            await discoveryTransport.BroadcastAsync(requestData, timeout, cts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            discoveryTransport.MessageReceived -= OnResponseReceived;
        }
    }

    private void OnResponseReceived(object? sender, DiscoveryRawMessageReceivedEventArgs e)
    {
        try
        {
            var message = serializer.Deserialize(e.Data);

            if (message is not DiscoveryResponseMessage response)
            {
                return;
            }

            var newRooms = response.RoomDiscoveryInfos
                .Where(r => discoveredRoomIds.Add(r.Room.Id))
                .ToList();

            if (newRooms.Count == 0)
            {
                return;
            }

            logger.Log($"Нашёл +{newRooms.Count} комнат");
            eventBus.PublishAsync(new RoomDiscoveredEvent()
            {
                RoomDiscoveryInfos = newRooms,
            });
        }
        catch (Exception ex)
        {
            logger.Log($"Error parsing discovery response: {ex.Message}");
        }
    }

    private async void OnRequestReceived(object? sender, DiscoveryRawMessageReceivedEventArgs e)
    {
        try
        {
            var message = serializer.Deserialize(e.Data);

            if (message is not DiscoveryRequestMessage)
            {
                return;
            }

            var discoveryResponse = new DiscoveryResponseMessage();

            foreach (var roomId in activeRoomIds)
            {
                var room = roomStore.GetRoom(roomId);

                if (room == null)
                {
                    logger.Log("Получил запрос на обнаружение, но комната не была установлена", LogLevel.Warning);
                    return;
                }

                discoveryResponse.RoomDiscoveryInfos.Add(new RoomDiscoveryInfo()
                {
                    Room = new RoomInfo(room),
                    Endpoints = room.ConnectionAddresses.Where(x => x.Origin == AddressOrigin.LAN && x.Type == TransportType.Tcp), // Должен быть, так как был настроен на это
                });
            }

            var data = serializer.Serialize(discoveryResponse);
            await e.Responder.RespondAsync(data, serviceCts.Token);
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка во время обработки запроса на обнаружение: {ex.Message}");
        }
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    async ValueTask IAsyncDisposable.DisposeAsync()
        => await discoveryTransport.StopListeningAsync();
}
