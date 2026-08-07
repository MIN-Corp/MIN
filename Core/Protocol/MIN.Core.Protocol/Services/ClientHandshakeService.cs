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

/// <inheritdoc cref="IClientHandshake"/>
public sealed class ClientHandshakeService : IClientHandshake
{
    private readonly ITransport transport;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ClientHandshakeService"/>
    /// </summary>
    public ClientHandshakeService(ITransport transport, ILoggerProvider logger)
    {
        this.transport = transport;
        this.logger = logger;
    }

    async Task<PreambleResult> IClientHandshake.HandleClientAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<PreambleResult>();

        void RawMessageReceivedHandler(object? sender, RawMessageReceivedEventArgs e)
        {
            if (e.ConnectionId != connectionId)
            {
                return;
            }
            transport.RawMessageReceived -= RawMessageReceivedHandler;

            var response = Encoding.UTF8.GetString(e.Data);
            if (!response.StartsWith(ProtocolConstants.ResponseStarter))
            {
                logger.Log($"Protocol client: неверный ответ от {connectionId}: {response[..Math.Min(response.Length, 20)]}");
                tcs.TrySetResult(new PreambleResult { IsSuccess = false, ErrorMessage = "Конечное подключение не соответсвует MIN протоколу" });
                return;
            }

            var roomInfo = JsonSerializer.Deserialize<RoomInfo>(response.AsSpan(ProtocolConstants.ResponseStarter.Length));

            if (roomInfo == null)
            {
                logger.Log($"Protocol client: не удалось десериализовать RoomInfo от {connectionId}");
                tcs.TrySetResult(new PreambleResult { IsSuccess = false, ErrorMessage = "Не удалось десериализовать информацию о комнате, возможно, вы на устаревшей версии" });
                return;
            }

            logger.Log($"Protocol client: успех, комната {roomInfo.Id} ({roomInfo.Name})");
            tcs.TrySetResult(new PreambleResult { IsSuccess = true, RoomInfo = roomInfo! });
        }

        transport.RawMessageReceived += RawMessageReceivedHandler;

        void ConnectionStateChangedHandler(object? sender, ConnectionStateChangedEventArgs e)
        {
            if (e.ConnectionId != connectionId)
            {
                return;
            }

            transport.ConnectionStateChanged -= ConnectionStateChangedHandler;

            if (!e.IsConnected && !tcs.Task.IsCompleted)
            {
                logger.Log("Protocol client: сервер разорвал соединения");
                tcs.TrySetResult(new PreambleResult { IsSuccess = false, ErrorMessage = "Конечное подключение не соответсвует MIN протоколу (Сервер разорвал соединение)" });
                return;
            }
        }

        transport.ConnectionStateChanged += ConnectionStateChangedHandler;

        var request = Encoding.UTF8.GetBytes(ProtocolConstants.ResponseStarter);
        logger.Log($"Protocol client: отправляю запрос на соединение {connectionId}");
        await Task.Delay(10, cancellationToken); // даём серверу время осознать
        await transport.SendAsync(request, connectionId, null, MessageChannel.Secure, cancellationToken);

        try
        {
            var timeout = TimeSpan.FromSeconds(ProtocolConstants.ClientSideTimeout);
            var result = await tcs.Task.WaitAsync(timeout, cancellationToken);
            return result;
        }
        catch (TimeoutException)
        {
            transport.RawMessageReceived -= RawMessageReceivedHandler;
            transport.ConnectionStateChanged -= ConnectionStateChangedHandler;
            logger.Log($"Protocol client: таймаут ожидания ответа от {connectionId}");
            return new PreambleResult { IsSuccess = false, ErrorMessage = "Время ожидания ответа вышло" };
        }
        finally
        {
            transport.RawMessageReceived -= RawMessageReceivedHandler;
            transport.ConnectionStateChanged -= ConnectionStateChangedHandler;
        }
    }
}
