using System.Net;
using System.Net.Sockets;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Models;
using MIN.Core.Transport.UdpSockets.Helpers;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Transport.UdpSockets.Models;

/// <summary>
/// Логическое udp соединение: связка id соединения и удалённой точки.
/// Сам сокет (UdpClient) общий на сервер комнаты, этот класс — только адрес + отправка
/// </summary>
internal sealed class UdpSocketConnection : BaseConnection, IAsyncDisposable
{
    private readonly UdpClient client;
    private readonly ILoggerProvider logger;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    private IPEndPoint remoteEndPoint;
    private bool disposed;

    /// <summary>
    /// Удалённая точка подключения
    /// </summary>
    public string? RemoteEndPoint => remoteEndPoint.ToString();

    /// <summary>
    /// Активно ли соединение
    /// </summary>
    public override bool IsConnected => !disposed;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="UdpSocketConnection"/>
    /// </summary>
    public UdpSocketConnection(Guid id, IPEndPoint remoteEndPoint, UdpClient client, ILoggerProvider logger)
        : base(id)
    {
        this.remoteEndPoint = remoteEndPoint;
        this.client = client;
        this.logger = logger;
    }

    /// <summary>
    /// Обновляет удалённую точку по адресу источника последней датаграммы (важно для NAT)
    /// </summary>
    public void UpdateRemoteEndPoint(IPEndPoint endpoint) => remoteEndPoint = endpoint;

    /// <summary>
    /// Отправляет данные
    /// </summary>
    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (disposed)
        {
            throw new InvalidOperationException("Соединение уже закрыто");
        }

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var datagram = UdpMessage.Wrap(Id, data);
            await client.SendAsync(datagram, remoteEndPoint, cancellationToken);
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
        writeLock.Dispose();
        await Task.CompletedTask;
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public async ValueTask DisposeAsync() => await StopAsync(DisconnectReason.None);
}
