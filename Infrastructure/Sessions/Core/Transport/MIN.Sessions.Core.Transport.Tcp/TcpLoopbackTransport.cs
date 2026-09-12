using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using MIN.Sessions.Core.Transport.Contracts.Events;
using MIN.Sessions.Core.Transport.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Transport.Tcp;

/// <inheritdoc cref="ISessionProcessTransport"/> на основе TCP Loopback
public sealed class TcpLoopbackTransport : ISessionProcessTransport
{
    private readonly ConcurrentDictionary<ProcessContext, TcpClient> connections = [];
    private readonly ConcurrentDictionary<ProcessContext, SemaphoreSlim> writeLocks = [];
    private readonly ConcurrentDictionary<ProcessContext, SemaphoreSlim> readLocks = [];
    private readonly CancellationTokenSource cts = new();
    private TcpListener? listener;
    private int port;

    /// <inheritdoc />
    public event EventHandler<ProcessTransportMessageEventArgs>? MessageReceived;

    Task ISessionProcessTransport.StartAsync(Guid roomId, CancellationToken ct)
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return Task.CompletedTask;
    }

    string ISessionProcessTransport.GetConnectionString() =>
        JsonSerializer.Serialize(new ConnectionInfo
        {
            Type = "tcp",
            Value = $"127.0.0.1:{port}",
        });

    async Task ISessionProcessTransport.WaitForConnectionAsync(
        ProcessContext context, int timeOutMs, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeOutMs);
        try
        {
            var client = await listener!.AcceptTcpClientAsync(timeoutCts.Token).ConfigureAwait(false);
            connections[context] = client;
            writeLocks[context] = new(1, 1);
            readLocks[context] = new(1, 1);
            _ = ReadLoopAsync(context, client.GetStream(), cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    bool ISessionProcessTransport.IsConnectionExists(ProcessContext context)
        => connections.ContainsKey(context);

    private async Task ReadLoopAsync(ProcessContext context, NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            var lengthBuf = new byte[4];
            while (!cancellationToken.IsCancellationRequested && readLocks.TryGetValue(context, out var readlock))
            {
                await readlock.WaitAsync(cancellationToken).ConfigureAwait(false);

                byte[] body;

                try
                {
                    await stream.ReadExactlyAsync(lengthBuf, cancellationToken).ConfigureAwait(false);
                    var length = BitConverter.ToInt32(lengthBuf);
                    body = new byte[length];
                    await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    readlock.Release();
                }

                MessageReceived?.Invoke(this, new ProcessTransportMessageEventArgs
                {
                    Context = context,
                    Data = body,
                });
            }
        }
        catch (EndOfStreamException) { }
        catch (OperationCanceledException) { }
        finally
        {
            connections.TryRemove(context, out _);
        }
    }

    async Task ISessionProcessTransport.SendAsync(byte[] data, ProcessContext context, CancellationToken cancellationToken)
    {
        if (connections.TryGetValue(context, out var client) && writeLocks.TryGetValue(context, out var writeLock))
        {
            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            var stream = client.GetStream();

            try
            {
                var lengthBuf = BitConverter.GetBytes(data.Length);
                await stream.WriteAsync(lengthBuf, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                writeLock.Release();
            }
        }
    }

    Task ISessionProcessTransport.DisconnectAsync(ProcessContext context)
    {
        connections.TryRemove(context, out _);
        writeLocks.TryRemove(context, out _);
        readLocks.TryRemove(context, out _);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);

        foreach (var server in connections.Values)
        {
            server.Dispose();
        }
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    async ValueTask IAsyncDisposable.DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
