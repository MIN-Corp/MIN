using System.Net.Sockets;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Models;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Transport.TcpSockets.Models;

/// <summary>
/// Соединение через Tcp Socket
/// </summary>
internal sealed class TcpSocketConnection : BaseConnection, IAsyncDisposable
{
    private readonly TcpClient client;
    private readonly ILoggerProvider logger;
    private readonly NetworkStream stream;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private Task? receiveLoop;
    private DisconnectReason disconnectReason = DisconnectReason.None;
    private bool disposed;

    /// <summary>
    /// Инициализирует новый экзмепляр <see cref="TcpSocketConnection"/>
    /// </summary>
    public TcpSocketConnection(TcpClient client, ILoggerProvider logger, Guid? connectionId = null)
        : base(connectionId ?? Guid.NewGuid())
    {
        this.client = client;
        this.logger = logger;
        stream = client.GetStream();
    }

    /// <summary>
    /// Удалённая точка подключения
    /// </summary>
    public string? RemoteEndPoint => client.Client.RemoteEndPoint?.ToString();

    /// <summary>
    /// Активно ли соединение
    /// </summary>
    public override bool IsConnected => client.Connected && stream != null && !disposed;

    /// <summary>
    /// Событие получения сообщения
    /// </summary>
    public event Action<TcpSocketConnection, byte[]>? RawMessageReceived;

    /// <summary>
    /// Событие отключения
    /// </summary>
    public event Action<TcpSocketConnection, DisconnectReason>? Disconnected;

    /// <summary>
    /// Запускает асинхронное чтение сообщений из Tcp socket
    /// </summary>
    public void StartReading()
    {
        receiveLoop = Task.Run(ReceiveLoopAsync);
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested && IsConnected)
            {
                var message = await ReadMessageAsync(cancellationTokenSource.Token);
                OnRawMessageReceived(message);
            }
        }
        catch (OperationCanceledException) { }
        catch (EndOfStreamException) { }
        catch (IOException) { }
        catch (Exception ex)
        {
            logger.Log($"Произошла ошибка {ex.GetType().Name} в tcp connection: {ex.Message}");
            disconnectReason = DisconnectReason.Error;
        }
        finally
        {
            OnDisconnected(disconnectReason);
            await DisposeAsync();
        }
    }

    static internal async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        var lengthBuf = new byte[4];
        var bytesRead = 0;
        while (bytesRead < 4)
        {
            var read = await stream.ReadAsync(lengthBuf.AsMemory(bytesRead, 4 - bytesRead), ct);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }
            bytesRead += read;
        }
        var length = BitConverter.ToInt32(lengthBuf);

        var data = new byte[length];
        bytesRead = 0;
        while (bytesRead < length)
        {
            var read = await stream.ReadAsync(data.AsMemory(bytesRead, length - bytesRead), ct);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }
            bytesRead += read;
        }
        return data;
    }

    private async Task<byte[]> ReadMessageAsync(CancellationToken ct)
        => await ReadFrameAsync(stream, ct);

    /// <summary>
    /// Отправляет данные
    /// </summary>
    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Соединение уже закрыто");
        }

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var lengthPrefix = BitConverter.GetBytes(data.Length);
            await stream.WriteAsync(lengthPrefix, cancellationToken);
            await stream.WriteAsync(data, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private void OnRawMessageReceived(byte[] data)
    {
        RawMessageReceived?.Invoke(this, data);
    }

    private void OnDisconnected(DisconnectReason reason)
    {
        Disconnected?.Invoke(this, reason);
    }

    public async ValueTask StopAsync(DisconnectReason reason)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        disconnectReason = reason;
        cancellationTokenSource.Cancel();

        if (receiveLoop != null)
        {
            await receiveLoop.WaitAsync(TimeSpan.FromSeconds(3));
        }

        stream.Dispose();
        client.Dispose();
        writeLock.Dispose();
        cancellationTokenSource.Dispose();
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync(DisconnectReason.None);
    }
}
