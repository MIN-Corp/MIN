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
                tcs.TrySetResult(new PreambleResult { IsSuccess = false, ErrorMessage = "Конечное подключение не соответствует MIN протоколу" });
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
            tcs.TrySetResult(new PreambleResult { IsSuccess = true, RoomInfo = roomInfo });
        }

        transport.RawMessageReceived += RawMessageReceivedHandler;

        void ConnectionStateChangedHandler(object? sender, ConnectionStateChangedEventArgs e)
        {
            if (e.ConnectionId != connectionId)
            {
                return;
            }

            transport.ConnectionStateChanged -= ConnectionStateChangedHandler;

            if (e.IsConnected || tcs.Task.IsCompleted)
            {
                return;
            }

            logger.Log("Protocol client: сервер разорвал соединения");
            tcs.TrySetResult(new PreambleResult { IsSuccess = false, ErrorMessage = "Конечное подключение не соответствует MIN протоколу (Сервер разорвал соединение)" });
        }

        transport.ConnectionStateChanged += ConnectionStateChangedHandler;

        try
        {
            var request = Encoding.UTF8.GetBytes(ProtocolConstants.ResponseStarter);
            logger.Log($"Protocol client: отправляю запрос на соединение {connectionId}");
            await Task.Delay(ProtocolConstants.ClientSideWarmupDelayMs, cancellationToken); // даём серверу время осознать

            for (var attempt = 0; attempt < ProtocolConstants.ClientSideRetryAmount; attempt++)
            {
                await transport.SendAsync(request, connectionId, null, MessageChannel.Secure, cancellationToken);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(ProtocolConstants.ClientSidePerTryTimeout));

                try
                {
                    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, cts.Token));
                    if (completedTask == tcs.Task)
                    {
                        var result = await tcs.Task;
                        return result;
                    }
                    logger.Log($"Protocol client: таймаут ожидания ответа от {connectionId}, попытка {attempt + 1}");
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    logger.Log($"Protocol client: таймаут ожидания ответа от {connectionId}, попытка {attempt + 1}");
                }

                if (attempt >= ProtocolConstants.ClientSideRetryAmount - 1)
                {
                    continue;
                }

                var delaySeconds = attempt + 1;
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cts.Token);
            }

            logger.Log($"Protocol client: все попытки истекли, соединение {connectionId} не отвечает");
            return new PreambleResult { IsSuccess = false, ErrorMessage = "Время ожидания ответа вышло после всех попыток" };
        }
        finally
        {
            transport.RawMessageReceived -= RawMessageReceivedHandler;
            transport.ConnectionStateChanged -= ConnectionStateChangedHandler;
        }
    }
}
