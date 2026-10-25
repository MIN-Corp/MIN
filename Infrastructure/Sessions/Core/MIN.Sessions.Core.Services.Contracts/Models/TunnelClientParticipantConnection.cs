using System.Net.Sockets;

namespace MIN.Sessions.Core.Services.Contracts.Models;

/// <summary>
/// Соединение клиента внутри тунеля
/// </summary>
public sealed class TunnelClientParticipantConnection : IDisposable
{
    private readonly TcpClient client;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    /// <summary>
    /// Поток данных
    /// </summary>
    public NetworkStream Stream { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TunnelClientParticipantConnection"/>
    /// </summary>
    public TunnelClientParticipantConnection(TcpClient client)
    {
        this.client = client;
        Stream = client.GetStream();
    }

    /// <summary>
    /// Отправить данные
    /// </summary>
    public async Task WriteAsync(byte[] payload)
    {
        await writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await Stream.WriteAsync(payload).ConfigureAwait(false);
            await Stream.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        writeLock.Dispose();
        client.Dispose();
    }
}
