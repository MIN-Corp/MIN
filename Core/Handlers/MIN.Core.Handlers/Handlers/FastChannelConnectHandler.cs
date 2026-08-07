using MIN.Core.Handlers.Contracts;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.FastChannelConnect;
using MIN.Core.Messaging.Stateless.RoomRelated.Join;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class FastChannelConnectHandler : IMessageHandler
{
    private readonly ITransport transport;
    private readonly IRoomConnectionRegistry registry;
    private readonly ILoggerProvider logger;

    public FastChannelConnectHandler(ITransport transport,
        IRoomConnectionRegistry registry,
        ILoggerProvider logger)
    {
        this.transport = transport;
        this.registry = registry;
        this.logger = logger;
    }

    IEnumerable<MessageTypeTag> IMessageHandler.HandledTypes
        => [MessageTypeTag.FastChannelConnectRequest, MessageTypeTag.FastChannelConnectResponse];

    int IMessageHandler.Priority => 3;

    async Task<HandlerResult> IMessageHandler.HandleAsync(IMessage message, MessageContext context)
    {
        if (message is FastChannelConnectRequestMessage requestMessage)
        {
            var serverConnectionId = registry.GetServerConnectionIdByRoomId(context.RoomContext.RoomId);
            var udpEndpoint = transport.GetEndpoints(serverConnectionId)
                .FirstOrDefault(ep => ep.Type == TransportType.Udp);

            if (udpEndpoint == null)
            {
                // остаёмся на TCP
                return HandlerResult.Success();
            }

            return HandlerResult.WithResponse(new FastChannelConnectResponseMessage { FastChannelEndpoint = udpEndpoint });
        }
        else if (message is FastChannelConnectResponseMessage response)
        {
            await transport.ConnectAsync(response.FastChannelEndpoint, context.ConnectionId, context.CancellationToken);
            return HandlerResult.WithResponse(new RoomJoinRequestMessage());
        }

        return HandlerResult.Failure($"Неизвестный тип сообщения в {nameof(FastChannelConnectHandler)} - {message.GetType()}");
    }
}
