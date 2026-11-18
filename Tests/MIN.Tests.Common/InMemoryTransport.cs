using System.Collections.Concurrent;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;

namespace MIN.Tests.Common;

/// <summary>
/// Запись об отправке датаграммы
/// </summary>
public sealed record SentDatagram(byte[] Data, Guid RecipientConnectionId);

/// <summary>
/// Транспорт в памяти: складывает отправления в очередь и позволяет доставлять байты вручную
/// </summary>
public sealed class InMemoryTransport : ITransport, IAsyncDisposable
{
    /// <inheritdoc />
    public event EventHandler<RawMessageReceivedEventArgs>? RawMessageReceived;

    /// <inheritdoc />
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Все отправленные датаграммы
    /// </summary>
    public ConcurrentQueue<SentDatagram> SentDatagrams { get; } = new();

    /// <summary>
    /// Обработчик отправки: позволяет маршрутизировать байты получателю в том же тесте
    /// </summary>
    public Func<SentDatagram, Task>? SendHandler { get; set; }

    /// <summary>
    /// Адреса, возвращаемые из GetEndpoints и SetUpEndpoints
    /// </summary>
    public List<IEndpoint> Endpoints { get; set; } = [];

    /// <summary>
    /// Идентификатор последнего запущенного хостинга
    /// </summary>
    private Guid HostingId { get; set; }

    /// <summary>
    /// Идентификаторы выполненных подключений
    /// </summary>
    public ConcurrentQueue<Guid> Connections { get; } = new();

    /// <inheritdoc />
    public Task<Guid> StartHostingAsync(Guid? serverConnectionId, ushort? preferredPort, int? sequentialAttempts, CancellationToken cancellationToken)
    {
        HostingId = serverConnectionId ?? Guid.NewGuid();
        return Task.FromResult(HostingId);
    }

    /// <inheritdoc />
    public Task StopHostingAsync(Guid connectionId)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Guid> ConnectAsync(IEndpoint endpoint, Guid? connectionId, CancellationToken cancellationToken)
    {
        var id = connectionId ?? Guid.NewGuid();
        Connections.Enqueue(id);
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public async Task SendAsync(ReadOnlyMemory<byte> data, Guid recipientConnectionId, Guid? serverConnectionId, MessageChannel channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var datagram = new SentDatagram(data.ToArray(), recipientConnectionId);
        SentDatagrams.Enqueue(datagram);
        if (SendHandler != null)
        {
            await SendHandler(datagram);
        }
    }

    /// <inheritdoc />
    public Task BroadcastAsync(ReadOnlyMemory<byte> data, Guid connectionId, IEnumerable<Guid>? excludeConnections, MessageChannel channel, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IEnumerable<IEndpoint>> SetUpEndpoints(Guid connectionId, NetworkOptions networkOptions, NetworkOptions? oldNetworkOptions, CancellationToken cancellationToken)
        => Task.FromResult<IEnumerable<IEndpoint>>(Endpoints);

    /// <inheritdoc />
    public IEnumerable<IEndpoint> GetEndpoints(Guid serverConnectionId) => Endpoints;

    /// <inheritdoc />
    public Task DisconnectClientAsync(Guid clientConnectionId, Guid? serverConnectionId, DisconnectReason reason)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(Guid connectionId, DisconnectReason reason)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Доставить байты так, будто они пришли от соединения
    /// </summary>
    public void Deliver(byte[] data, Guid connectionId, Guid? serverConnectionId = null)
        => RawMessageReceived?.Invoke(this, new RawMessageReceivedEventArgs(data, connectionId, serverConnectionId));

    /// <summary>
    /// Поднять событие изменения состояния соединения
    /// </summary>
    public void RaiseConnectionStateChanged(ConnectionStateChangedEventArgs args)
        => ConnectionStateChanged?.Invoke(this, args);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
