using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using MIN.Sessions.Core.Transport.Contracts.Events;
using MIN.Sessions.Core.Transport.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Transport.NamedPipes;

/// <inheritdoc cref="ISessionProcessTransport"/> на основе Named Pipes (Только для Windows)
public sealed class NamedPipeProcessTransport : ISessionProcessTransport
{
    private readonly ConcurrentDictionary<ProcessContext, NamedPipeServerStream> connections = [];
    private readonly ConcurrentDictionary<ProcessContext, SemaphoreSlim> writeLocks = [];
    private readonly ConcurrentDictionary<ProcessContext, SemaphoreSlim> readLocks = [];
    private readonly CancellationTokenSource cts = new();
    private string pipeName = string.Empty;

    /// <inheritdoc/>
    public event EventHandler<ProcessTransportMessageEventArgs>? MessageReceived;

    Task ISessionProcessTransport.StartAsync(Guid roomId, CancellationToken cancellationToken)
    {
        pipeName = $"MIN_{roomId}";
        return Task.CompletedTask;
    }

    string ISessionProcessTransport.GetConnectionString() =>
        JsonSerializer.Serialize(new ConnectionInfo
        {
            Type = "pipe",
            Value = pipeName,
        });

    async Task ISessionProcessTransport.WaitForConnectionAsync(ProcessContext context, int timeOutMs, CancellationToken cancellationToken)
    {
        var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options: PipeOptions.Asynchronous);

        var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectionCts.CancelAfter(timeOutMs);
        try
        {
            await server.WaitForConnectionAsync(connectionCts.Token).ConfigureAwait(false);

            connections.TryAdd(context, server);
            writeLocks.TryAdd(context, new(1, 1));
            readLocks.TryAdd(context, new(1, 1));
            _ = ReadLoopAsync(context, server, cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    bool ISessionProcessTransport.IsConnectionExists(ProcessContext context)
        => connections.ContainsKey(context);

    private async Task ReadLoopAsync(ProcessContext context, NamedPipeServerStream stream, CancellationToken cancellationToken)
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
        if (connections.TryGetValue(context, out var stream) && writeLocks.TryGetValue(context, out var writeLock))
        {
            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var lengthBuf = BitConverter.GetBytes(data.Length);
                await stream.WriteAsync(lengthBuf, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
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
