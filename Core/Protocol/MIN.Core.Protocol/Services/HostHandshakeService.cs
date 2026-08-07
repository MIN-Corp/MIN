using System.Text;
using System.Text.Json;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Protocol.Contracts.Constants;
using MIN.Core.Protocol.Contracts.Interfaces;
using MIN.Core.Protocol.Contracts.Models;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Protocol.Services;

/// <inheritdoc cref="IHostHandshake"/>
public sealed class HostHandshakeService : IHostHandshake
{
    private readonly ITransport transport;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="HostHandshakeService"/>
    /// </summary>
    public HostHandshakeService(ITransport transport,
        ILoggerProvider logger)
    {
        this.transport = transport;
        this.logger = logger;
    }

    async Task<PreambleResult> IHostHandshake.HandleServerAsync(Guid serverConnectionId, Guid clientConnectionId, RoomInfo roomInfo, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<PreambleResult>();

        logger.Log($"Protocol server: ожидаю запрос от {clientConnectionId}");

        async void Handler(object? sender, RawMessageReceivedEventArgs e)
        {
            if (e.ConnectionId != clientConnectionId)
            {
                return;
            }
            transport.RawMessageReceived -= Handler;

            var request = Encoding.UTF8.GetString(e.Data);
            if (request != ProtocolConstants.ResponseStarter)
            {
                logger.Log($"Protocol server: неверный протокол от {clientConnectionId}");
                tcs.TrySetResult(new PreambleResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Неверный протокол"
                });
                return;
            }

            logger.Log($"Protocol server: клиент {clientConnectionId} прошёл протокол");

            var roomJson = JsonSerializer.Serialize(roomInfo);
            var response = Encoding.UTF8.GetBytes(ProtocolConstants.ResponseStarter + roomJson);
            await transport.SendAsync(response, clientConnectionId, serverConnectionId, MessageChannel.Secure, cancellationToken);

            tcs.TrySetResult(new PreambleResult { IsSuccess = true });
        }

        transport.RawMessageReceived += Handler;

        try
        {
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(ProtocolConstants.ServerSideTimuout), cancellationToken);
        }
        catch (TimeoutException)
        {
            transport.RawMessageReceived -= Handler;
            logger.Log($"Protocol server: таймаут ожидания запроса от {clientConnectionId}");
            return new PreambleResult { IsSuccess = false, ErrorMessage = "Время ожидания запроса вышло" };
        }
    }
}
