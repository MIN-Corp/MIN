using System.Net;
using System.Net.Sockets;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.UdpSockets.Helpers;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Transport.UdpSockets.Client;

/// <summary>
/// Клиент Udp для подключения к удалённой комнате
/// </summary>
internal sealed class UdpSocketClient : IAsyncDisposable
{
    private readonly ILoggerProvider logger;
    private readonly UdpClient client = new();
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private IPEndPoint? hostEndPoint;
    private Task? receiveLoop;
    private bool disposed;

    /// <summary>
    /// Событие получения сообщения
    /// </summary>
    public event Action<byte[]>? OnMessageReceived;

    /// <summary>
    /// Идентификатор соединения. Один на обе ноги (TCP+UDP) — задаёт ChannelTransport
    /// </summary>
    public Guid ConnectionId { get; }

    /// <summary>
    /// Локальная точка (уйдёт в UdpAnnounceMessage)
    /// </summary>
    public string? LocalEndPoint => client.Client.LocalEndPoint?.ToString();

    public UdpSocketClient(Guid connectionId, ILoggerProvider logger)
    {
        ConnectionId = connectionId;
        this.logger = logger;
    }

    /// <summary>
    /// Подключиться к серверу. Для UDP это локальная операция — сетевого рукопожатия нет
    /// </summary>
    public async Task ConnectAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        hostEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        var handshakeDatagram = UdpMessage.Wrap(ConnectionId, []);
        await client.SendAsync(handshakeDatagram, hostEndPoint, cancellationToken);
        receiveLoop = Task.Run(ReceiveLoopAsync, cancellationToken);
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync(cancellationTokenSource.Token);
                var (senderId, payload) = UdpMessage.Parse(result.Buffer);

                if (senderId != ConnectionId)
                {
                    continue;
                }

                OnMessageReceived?.Invoke(payload);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
        catch (Exception ex)
        {
            logger.Log($"Произошла ошибка {ex.GetType().Name} в udp клиенте: {ex.Message}");
        }
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (hostEndPoint == null)
        {
            throw new InvalidOperationException("Not connected");
        }

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var datagram = UdpMessage.Wrap(ConnectionId, data);
            await client.SendAsync(datagram, hostEndPoint, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async ValueTask StopAsync(DisconnectReason reason)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellationTokenSource.Cancel();

        if (receiveLoop != null)
        {
            await receiveLoop.WaitAsync(TimeSpan.FromSeconds(3));
        }

        writeLock.Dispose();
        cancellationTokenSource.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(DisconnectReason.None);
        client.Dispose();
    }
}
