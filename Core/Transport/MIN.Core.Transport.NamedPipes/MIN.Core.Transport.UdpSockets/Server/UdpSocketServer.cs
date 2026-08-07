using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Helpers;
using MIN.Core.Transport.UdpSockets.Helpers;
using MIN.Core.Transport.UdpSockets.Models;
using MIN.Helpers.Contracts.Interfaces;
using Open.Nat;

namespace MIN.Core.Transport.UdpSockets.Server;

/// <summary>
/// Сервер Udp Socket для комнаты.
/// Один сокет, логические соединения по id из датаграмм
/// </summary>
internal sealed class UdpSocketServer : IAsyncDisposable
{
    private readonly ILoggerProvider logger;
    private readonly UdpClient listener;
    private readonly ConcurrentDictionary<Guid, UdpSocketConnection> connections = new();

    private CancellationTokenSource? cts;
    private Task? receiveLoop;

    /// <summary>
    /// Порт подключения
    /// </summary>
    public ushort Port => (ushort)((IPEndPoint)listener.Client.LocalEndPoint!).Port;

    /// <summary>
    /// Текущие соединения
    /// </summary>
    public IReadOnlyDictionary<Guid, UdpSocketConnection> Connections => connections;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="UdpSocketServer"/>
    /// </summary>
    public UdpSocketServer(ILoggerProvider logger, int port)
    {
        this.logger = logger;
        listener = new UdpClient(new IPEndPoint(IPAddress.Any, port));
    }

    /// <summary>
    /// Событие получения сообщения от соединения
    /// </summary>
    public event Action<UdpSocketServer, (UdpSocketConnection Connection, byte[] Message)>? OnMessageReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        receiveLoop = Task.Run(ReceiveLoopAsync, cts.Token);
        logger.Log($"Стартанул udp сервер на порту: {Port}");
        return Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!cts!.Token.IsCancellationRequested)
            {
                var result = await listener.ReceiveAsync(cts.Token);
                var (connectionId, payload) = UdpMessage.Parse(result.Buffer);

                var connection = connections.GetOrAdd(connectionId, _ =>
                {
                    var conn = new UdpSocketConnection(connectionId, result.RemoteEndPoint, listener, logger);
                    logger.Log($"Клиент подключился (udp): {result.RemoteEndPoint}");
                    return conn;
                });

                // На NAT адрес источника — единственный, куда реально можно слать
                connection.UpdateRemoteEndPoint(result.RemoteEndPoint);

                if (payload.Length > 0)
                {
                    OnMessageReceived?.Invoke(this, (connection, payload));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.Log($"Произошла ошибка во время приёма udp: {ex.Message}");
        }
    }

    /// <summary>
    /// Разорвать логическое соединение (физически отключить удалённый UdpClient нельзя)
    /// </summary>
    public async Task DisconnectConnectionAsync(Guid connectionId, DisconnectReason reason)
    {
        if (connections.TryRemove(connectionId, out var connection))
        {
            await connection.StopAsync(reason);
        }
    }

    public async ValueTask DisposeAsync()
    {
        cts?.Cancel();

        await PortForwardingHelper.UnmapPortAsync(Port, Protocol.Udp);

        if (receiveLoop != null)
        {
            await receiveLoop.WaitAsync(TimeSpan.FromSeconds(5));
        }

        listener.Dispose();
        connections.Clear();
        cts?.Dispose();
    }
}
