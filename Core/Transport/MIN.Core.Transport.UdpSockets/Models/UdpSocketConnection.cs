using System.Net;
using System.Net.Sockets;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Models;
using MIN.Core.Transport.UdpSockets.Helpers;

namespace MIN.Core.Transport.UdpSockets.Models;

/// <summary>
/// Логическое udp соединение: связка id соединения и удалённой точки.
/// Сам сокет (UdpClient) общий на сервер комнаты, этот класс — только адрес + отправка
/// </summary>
internal sealed class UdpSocketConnection : BaseConnection, IAsyncDisposable
{
    private readonly UdpClient client;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    private IPEndPoint remoteEndPoint;
    private byte[] datagramBuffer = [];
    private bool disposed;

    /// <summary>
    /// Активно ли соединение
    /// </summary>
    public override bool IsConnected => !disposed;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="UdpSocketConnection"/>
    /// </summary>
    public UdpSocketConnection(Guid id, IPEndPoint remoteEndPoint, UdpClient client)
        : base(id)
    {
        this.remoteEndPoint = remoteEndPoint;
        this.client = client;
    }

    /// <summary>
    /// Обновляет удалённую точку по адресу источника последней датаграммы (важно для NAT)
    /// </summary>
    public void UpdateRemoteEndPoint(IPEndPoint endpoint) => remoteEndPoint = endpoint;

    /// <summary>
    /// Отправляет данные
    /// </summary>
    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (disposed)
        {
            throw new InvalidOperationException("Соединение уже закрыто");
        }

        await writeLock.WaitAsync(cancellationToken);

        try
        {
            var total = UdpMessage.ConnectionIdSize + data.Length;
            if (datagramBuffer.Length < total)
            {
                datagramBuffer = new byte[total];
            }

            UdpMessage.Wrap(Id, data.Span, datagramBuffer);
            await client.SendAsync(datagramBuffer.AsMemory(0, total), remoteEndPoint, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public ValueTask StopAsync(DisconnectReason reason)
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        disposed = true;
        writeLock.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public async ValueTask DisposeAsync() => await StopAsync(DisconnectReason.None);
}
