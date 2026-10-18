using System.Net.Sockets;

namespace MIN.Sessions.Core.Services.Contracts.Models;

/// <summary>
/// Соединение как хоста внутри тунеля
/// </summary>
public sealed class TunnelServerParticipantConnection : IDisposable
{
    private readonly TcpClient client;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    /// <summary>
    /// Идентификатор участника
    /// </summary>
    public Guid ParticipantId { get; }

    /// <summary>
    /// Поток данных
    /// </summary>
    public NetworkStream Stream { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TunnelServerParticipantConnection"/>
    /// </summary>
    public TunnelServerParticipantConnection(Guid participantId, TcpClient client)
    {
        this.client = client;
        ParticipantId = participantId;
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
