using System.Collections.Concurrent;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Helpers;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;
using MIN.Core.Transport.UdpSockets.Client;
using MIN.Core.Transport.UdpSockets.Models;
using MIN.Core.Transport.UdpSockets.Server;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using Open.Nat;

namespace MIN.Core.Transport.UdpSockets;

/// <summary>
/// Реализация передачи данных на основе Udp Socket
/// </summary>
public class UdpTransport : IAsyncDisposable
{
    private readonly ILoggerProvider logger;
    private readonly ConcurrentDictionary<Guid, UdpSocketServer> servers = new();
    private readonly ConcurrentDictionary<Guid, UdpSocketClient> clients = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<IEndpoint>> cachedServerEndpoints = new();

    /// <summary>
    /// Получено сырое сообщение
    /// </summary>
    public event EventHandler<RawMessageReceivedEventArgs>? RawMessageReceived;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="UdpTransport"/>
    /// </summary>
    public UdpTransport(ILoggerProvider logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Начать хостинг
    /// </summary>
    public async Task<Guid> StartHostingAsync(Guid? serverConnectionId, ushort? preferredPort, int? sequentialAttempts, CancellationToken cancellationToken)
    {
        var connectionId = serverConnectionId ?? Guid.NewGuid();
        var port = PortProvider.AllocatePort(TransportType.Udp, preferredPort, sequentialAttempts);
        var server = new UdpSocketServer(logger, port);

        server.OnMessageReceived += (UdpSocketServer _, (UdpSocketConnection conn, byte[] msg) eventArgs) =>
        {
            var args = new RawMessageReceivedEventArgs(eventArgs.msg, eventArgs.conn.Id, connectionId);
            RawMessageReceived?.Invoke(this, args);
        };

        await server.StartAsync(cancellationToken);
        servers.TryAdd(connectionId, server);

        return connectionId;
    }

    /// <summary>
    /// Остановить хостинг
    /// </summary>
    public async Task StopHostingAsync(Guid connectionId)
    {
        if (servers.TryRemove(connectionId, out var server))
        {
            await server.DisposeAsync();
            PortProvider.ReleasePort(server.Port, TransportType.Udp);
        }
    }

    /// <summary>
    /// Подключиться
    /// </summary>
    public async Task<Guid> ConnectAsync(IEndpoint endpoint, Guid? connectionId, CancellationToken cancellationToken)
    {
        if (endpoint is not UdpEndpoint udpEp)
        {
            throw new ArgumentException("Endpoint must be UdpEndpoint");
        }

        var client = new UdpSocketClient(connectionId ?? Guid.NewGuid(), logger);   // id задаст ChannelTransport
        client.OnMessageReceived += msg =>
        {
            var args = new RawMessageReceivedEventArgs(msg, client.ConnectionId);
            RawMessageReceived?.Invoke(this, args);
        };

        await client.ConnectAsync(udpEp.IpAddress, udpEp.Port, cancellationToken);
        clients.TryAdd(client.ConnectionId, client);

        return client.ConnectionId;
    }

    /// <summary>
    /// Отправить данные
    /// </summary>
    public async Task SendAsync(byte[] data, Guid recipientConnectionId, Guid? serverConnectionId, CancellationToken cancellationToken)
    {
        if (clients.TryGetValue(recipientConnectionId, out var client))
        {
            await client.SendAsync(data, cancellationToken);
            return;
        }

        if (servers.TryGetValue(serverConnectionId ?? Guid.Empty, out var server) &&
            server.Connections.TryGetValue(recipientConnectionId, out var conn))
        {
            await conn.SendAsync(data, cancellationToken);
            return;
        }

        logger.Log("Попытка отправить сообщение, не подключившись, игнорю", LogLevel.Warning);
    }

    /// <summary>
    /// Разослать данные по всем соединениям
    /// </summary>
    public async Task BroadcastAsync(byte[] data, Guid connectionId, IEnumerable<Guid>? excludeConnections, CancellationToken cancellationToken)
    {
        var excludeSet = excludeConnections?.ToHashSet() ?? [];

        if (servers.TryGetValue(connectionId, out var server))
        {
            var tasks = server.Connections.Values
                .Where(conn => !excludeSet.Contains(conn.Id))
                .Select(conn => conn.SendAsync(data, cancellationToken));
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="connectionId"></param>
    /// <param name="networkOptions"></param>
    /// <param name="oldNetworkOptions"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<IEnumerable<IEndpoint>> SetUpEndpoints(Guid connectionId, NetworkOptions networkOptions, NetworkOptions? oldNetworkOptions, CancellationToken cancellationToken)
    {
        if (!servers.TryGetValue(connectionId, out var server))
        {
            throw new InvalidOperationException($"Connection {connectionId} is not hosted locally");
        }

        var includeWan = false;

        if ((oldNetworkOptions == null && networkOptions.EnablePortForwarding)
            || (oldNetworkOptions.HasValue && networkOptions.EnablePortForwarding && !oldNetworkOptions.Value.EnablePortForwarding))
        {
            includeWan = true;

            ResultCodes? result = ResultCodes.UnknownError;
            try
            {
                result = await PortForwardingHelper.MapPortAsync(server.Port, Protocol.Udp, cancellationToken, $"Room {server.Port}");
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            switch (result)
            {
                case ResultCodes.Success:
                    logger.Log($"Порт проброшен. Публичный порт: {server.Port}");
                    break;

                case ResultCodes.ConflictInMappingEntry:
                    const string conflictMessage = "UPnP конфликтует с текущим портом. Клиенты из Интернета не могут подключиться, если порт не проброшен вручную. Попробуйте повторить попытку";
                    logger.Log(conflictMessage, LogLevel.Error);
                    throw new InvalidOperationException(conflictMessage);

                case ResultCodes.UnknownError:
                    const string message = "UPnP не доступен. Клиенты из Интернета не могут подключиться, если порт не проброшен вручную. Либо ну удалось получить публичный адрес.";
                    logger.Log(message, LogLevel.Error);
                    throw new InvalidOperationException(message);
                case null:
                    break;
            }
        }
        else if (oldNetworkOptions.HasValue && !networkOptions.EnablePortForwarding && oldNetworkOptions.Value.EnablePortForwarding)
        {
            await PortForwardingHelper.UnmapPortAsync(server.Port, Protocol.Udp, cancellationToken);
        }

        var includeVpns = oldNetworkOptions == null && networkOptions.EnableRadmin
            || oldNetworkOptions.HasValue && networkOptions.EnableRadmin && !oldNetworkOptions.Value.EnableRadmin;

        IEnumerable<MachineKnownIp> knownIps = [];

        try
        {
            knownIps = await NetworkHelper.GetAllKnownIpsAsync(includeWan, includeVpns, cancellationToken);
        }
        catch
        {
            if ((oldNetworkOptions == null && networkOptions.EnablePortForwarding)
                || (oldNetworkOptions.HasValue && networkOptions.EnablePortForwarding && !oldNetworkOptions.Value.EnablePortForwarding))
            {
                await PortForwardingHelper.UnmapPortAsync(server.Port, Protocol.Udp, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        var endpoints = knownIps.Select(ip =>
            new UdpEndpoint
            {
                Origin = ip.Origin,
                IpAddress = ip.Address.ToString(),
                NetworkName = ip.NetworkName,
                Port = server.Port
            }).ToList();

        cachedServerEndpoints[connectionId] = endpoints;

        return endpoints;
    }

    /// <summary>
    /// Получить точки подключения
    /// </summary>
    public IEnumerable<IEndpoint> GetEndpoints(Guid serverConnectionId)
    {
        if (!servers.ContainsKey(serverConnectionId))
        {
            throw new InvalidOperationException($"Connection {serverConnectionId} is not hosted locally");
        }

        return cachedServerEndpoints[serverConnectionId];
    }

    /// <summary>
    /// Отключить клиента
    /// </summary>
    public async Task DisconnectClientAsync(Guid clientConnectionId, Guid? serverConnectionId, DisconnectReason reason = DisconnectReason.None)
    {
        logger.Log($"Отключаю соединения с id {clientConnectionId}");
        if (servers.TryGetValue(serverConnectionId ?? Guid.Empty, out var server) &&
            server.Connections.TryGetValue(clientConnectionId, out _))
        {
            await server.DisconnectConnectionAsync(clientConnectionId, reason);
        }
        else if (clients.TryGetValue(clientConnectionId, out var client))
        {
            await client.StopAsync(reason);
            clients.TryRemove(clientConnectionId, out _);
        }
    }

    /// <summary>
    /// Отключиться самому
    /// </summary>
    public async Task DisconnectAsync(Guid connectionId, DisconnectReason reason)
        => await DisconnectClientAsync(connectionId, null, reason);

    /// <summary>
    /// Готов отправлять сообщения
    /// </summary>
    public bool IsReadyToSend(Guid recipientConnectionId, Guid? serverConnectionId)
    {
        if (clients.ContainsKey(recipientConnectionId))
        {
            return true;
        }

        return servers.TryGetValue(serverConnectionId ?? Guid.Empty, out var server)
            && server.Connections.ContainsKey(recipientConnectionId);
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public async ValueTask DisposeAsync()
    {
        foreach (var server in servers.Values)
        {
            await server.DisposeAsync();
        }
        foreach (var client in clients.Values)
        {
            await client.DisposeAsync();
        }
    }
}
