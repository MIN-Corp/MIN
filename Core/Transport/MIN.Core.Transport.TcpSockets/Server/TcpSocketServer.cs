using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MIN.Core.Transport.Contracts.Constants;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Helpers;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Helpers.Contracts.Interfaces;
using Open.Nat;

namespace MIN.Core.Transport.TcpSockets.Server;

/// <summary>
/// Сервер Tcp Socket для комнаты
/// </summary>
internal sealed class TcpSocketServer : IAsyncDisposable
{
    private readonly ILoggerProvider logger;
    private readonly TcpListener listener;
    private readonly SemaphoreSlim connectionSlots;
    private readonly ConcurrentDictionary<Guid, TcpSocketConnection> connections = new();
    private readonly int maxConnections = TransportConstants.RoomMaximumConnectionsAmount;

    private CancellationTokenSource? cts;
    private Task? acceptLoop;

    /// <summary>
    /// Порт подключения
    /// </summary>
    public ushort Port => (ushort)((IPEndPoint)listener.LocalEndpoint).Port;

    /// <summary>
    /// Текущие соединения
    /// </summary>
    public IReadOnlyDictionary<Guid, TcpSocketConnection> Connections => connections;

    /// <summary>
    /// Инициализирует новый экзмепляр <see cref="TcpSocketServer"/>
    /// </summary>
    public TcpSocketServer(ILoggerProvider logger, int port)
    {
        this.logger = logger;
        listener = new TcpListener(IPAddress.Any, port);
        connectionSlots = new SemaphoreSlim(maxConnections, maxConnections);
    }

    /// <summary>
    /// Событие, возникающее при получении сообщения от соединения
    /// </summary>
    public event Action<TcpSocketServer, (TcpSocketConnection Connection, byte[] Message)>? OnMessageReceived;

    /// <summary>
    /// Соеднинение сервера и клиента разорвалось
    /// </summary>
    public event Action<TcpSocketServer, (TcpSocketConnection Connection, DisconnectReason Reason)>? ConnectionDisconnected;

    /// <summary>
    /// Соеднинение сервера и клиента установилось
    /// </summary>
    public event Action<TcpSocketServer, TcpSocketConnection>? OnConnectionEstablished;

    /// <summary>
    /// Запустить сервер
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        listener.Start();
        acceptLoop = Task.Run(AcceptLoopAsync, cancellationToken);
        logger.Log($"Стартанул сервер на порту: {Port}");
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!cts!.Token.IsCancellationRequested)
            {
                await connectionSlots.WaitAsync(cts.Token);
                var tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
                _ = HandleConnectionAsync(tcpClient);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.Log($"Произошла ошибка во время принятия клиента: {ex.Message}");
        }
    }

    private async Task HandleConnectionAsync(TcpClient tcpClient)
    {
        TcpSocketConnection? connection = null;

        try
        {
            var stream = tcpClient.GetStream();
            var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString();

            Guid announcedId;
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts!.Token);
                timeoutCts.CancelAfter(ConnectionPreamble.Timeout);
                var preamble = await TcpSocketConnection.ReadFrameAsync(stream, timeoutCts.Token);

                if (!ConnectionPreamble.TryParse(preamble, out announcedId))
                {
                    logger.Log($"Клиент {remoteEndPoint ?? "unknown"} прислал невалидную преамбалу, отклоняю");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                logger.Log($"Клиент {remoteEndPoint ?? "unknown"} не прислал преамбалу за {ConnectionPreamble.Timeout}, отклоняю");
                return;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                logger.Log($"Клиент {remoteEndPoint ?? "unknown"} разорвал соединение до преамбалу: {ex.Message}");
                return;
            }

            logger.Log($"Клиент {remoteEndPoint ?? "unknown"} подключился с id {announcedId}");

            connection = new TcpSocketConnection(tcpClient, logger, announcedId);
            connection.RawMessageReceived += OnConnectionMessage;
            connection.Disconnected += OnConnectionDisconnected;
            connection.StartReading();

            connections.TryAdd(connection.Id, connection);
            OnConnectionEstablished?.Invoke(this, connection);

            var tcs = new TaskCompletionSource<bool>();
            connection.Disconnected += (_, _) => tcs.TrySetResult(true);
            await tcs.Task;
        }
        finally
        {
            if (connection != null)
            {
                connections.TryRemove(connection.Id, out _);
                await connection.DisposeAsync();
            }
            else
            {
                tcpClient.Dispose();
            }

            connectionSlots.Release();
        }
    }

    private void OnConnectionMessage(TcpSocketConnection conn, byte[] msg)
        => OnMessageReceived?.Invoke(this, (conn, msg));

    private void OnConnectionDisconnected(TcpSocketConnection conn, DisconnectReason reason)
        => ConnectionDisconnected?.Invoke(this, (conn, reason));

    public async ValueTask DisposeAsync()
    {
        cts?.Cancel();

        await PortForwardingHelper.UnmapPortAsync(Port, Protocol.Tcp);

        if (acceptLoop != null)
        {
            await acceptLoop.WaitAsync(TimeSpan.FromSeconds(5));
        }

        listener.Stop();

        foreach (var connection in connections.Values)
        {
            await connection.DisposeAsync();
        }

        connectionSlots.Dispose();
        cts?.Dispose();
    }
}
