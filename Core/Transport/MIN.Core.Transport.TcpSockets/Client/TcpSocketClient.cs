using System.Net.Sockets;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Helpers;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Transport.TcpSockets.Client;

/// <summary>
/// Клиент Tcp для подключения к удалённой комнате
/// </summary>
internal sealed class TcpSocketClient : IAsyncDisposable
{
    private readonly ILoggerProvider logger;

    private TcpClient? client;
    private TcpSocketConnection? connection;
    private bool disposed;

    /// <summary>
    /// Событие получения сообщения
    /// </summary>
    public event Action<byte[]>? OnMessageReceived;

    /// <summary>
    /// Событие отключения
    /// </summary>
    public event Action<DisconnectReason>? OnDisconnected;

    /// <summary>
    /// Идентификатор соеднинения
    /// </summary>
    public Guid ConnectionId => connection?.Id ?? Guid.Empty;

    /// <summary>
    /// Флаг подключения
    /// </summary>
    public bool IsConnected => connection?.IsConnected == true;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TcpSocketClient"/>
    /// </summary>
    public TcpSocketClient(ILoggerProvider logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Подключиться к серверу
    /// </summary>
    public async Task<Guid> ConnectAsync(string ipAddress, int port, Guid? connectionId, CancellationToken cancellationToken)
    {
        client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await client.ConnectAsync(ipAddress, port, cts.Token);

        connection = new TcpSocketConnection(client, logger, connectionId);
        connection.RawMessageReceived += (_, msg) => OnMessageReceived?.Invoke(msg);
        connection.Disconnected += (_, ex) => OnDisconnected?.Invoke(ex);

        await connection.SendAsync(ConnectionPreamble.Create(connection.Id), cancellationToken);

        connection.StartReading();

        return connection.Id;
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (connection == null)
        {
            throw new InvalidOperationException("Not connected");
        }
        await connection.SendAsync(data, cancellationToken);
    }

    public async ValueTask StopAsync(DisconnectReason reason)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (connection != null)
        {
            await connection.StopAsync(reason);
        }

        client?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(DisconnectReason.None);
    }
}
