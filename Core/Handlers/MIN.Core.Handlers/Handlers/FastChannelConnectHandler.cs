using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.FastChannelConnect;
using MIN.Core.Messaging.Stateless.RoomRelated.Join;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Extensions;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.UdpSockets.Models;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class FastChannelConnectHandler : BaseHandler
{
    private readonly ITransport transport;
    private readonly IRoomConnectionRegistry registry;
    private readonly IRoomStore roomStore;

    public FastChannelConnectHandler(ITransport transport,
        IRoomConnectionRegistry registry,
        IRoomStore roomStore,
        ILoggerProvider logger) : base(logger)
    {
        this.transport = transport;
        this.registry = registry;
        this.roomStore = roomStore;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.FastChannelConnectRequest, MessageTypeTag.FastChannelConnectResponse];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
        => message switch
        {
            FastChannelConnectRequestMessage requestMessage => HandleFastChannelRequest(requestMessage, context),
            FastChannelConnectResponseMessage response => await HandleFastChannelResponse(response, context),
            _ => throw new HandlerTypeMismatch(this, message)
        };

    private HandlerResult HandleFastChannelRequest(FastChannelConnectRequestMessage requestMessage, MessageContext context)
    {
        var serverConnectionId = registry.GetServerConnectionIdByRoomId(context.RoomContext.RoomId);
        var udpEndpoints = transport.GetEndpoints(serverConnectionId)
            .Where(ep => ep.Type == TransportType.Udp)
            .ToList();

        // Клиент проигнорирует IP из ответа и подключится по адресу своего TCP-соединения,
        // поэтому важен только порт: отдаём любой UDP endpoint комнаты, желательно с запрошенным Origin.
        // Ответ отправляется всегда — иначе клиент зависнет в ожидании FastChannelConnectResponse
        var udpEndpoint = udpEndpoints.FirstOrDefault(ep => ep.Origin == requestMessage.AddressOrigin)
            ?? udpEndpoints.FirstOrDefault();

        if (udpEndpoint == null)
        {
            LogInfo($"Нет UDP endpoint для быстрого канала комнаты {context.RoomContext.RoomId}, клиент останется на TCP");
        }

        return HandlerResult.WithResponse(new FastChannelConnectResponseMessage()
        {
            FastChannelEndpoint = udpEndpoint,
        });
    }

    private async Task<HandlerResult> HandleFastChannelResponse(FastChannelConnectResponseMessage response, MessageContext context)
    {
        var fastChannelEndpoint = response.FastChannelEndpoint;

        if (fastChannelEndpoint == null)
        {
            LogInfo("Хост не предоставил UDP endpoint, остаёмся на TCP");
            return HandlerResult.WithResponse(new RoomJoinRequestMessage());
        }

        var dialedAddress = roomStore.TryGetRoom(context.RoomContext.RoomId, out var savedRoom)
            ? savedRoom.ConnectionAddresses.FirstOrDefault()
            : null;

        // Подключаемся по адресу, который реально прошёл для TCP, и порту из ответа хоста
        fastChannelEndpoint = new UdpEndpoint()
        {
            Origin = fastChannelEndpoint.Origin,
            IpAddress = dialedAddress?.GetHost() ?? fastChannelEndpoint.GetHost(),
            Port = fastChannelEndpoint.GetPort(),
        };

        await transport.ConnectAsync(fastChannelEndpoint, context.ConnectionId, context.CancellationToken);

        return HandlerResult.WithResponse(new RoomJoinRequestMessage());
    }
}
