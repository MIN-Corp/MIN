using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;
using MIN.Core.Transport.TcpSockets;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Core.Transport.UdpSockets;
using MIN.Core.Transport.UdpSockets.Models;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Transport;

/// <summary>
/// Транспорт, способный передавать данные по двум каналам: Secure (TCP) и Fast (UDP)
/// </summary>
public class ChannelTransport : ITransport, IAsyncDisposable
{
    private readonly TcpTransport secureTransport;
    private readonly UdpTransport fastTransport;
    private readonly ILoggerProvider logger;

    /// <inheritdoc />
    public event EventHandler<RawMessageReceivedEventArgs>? RawMessageReceived;

    /// <inheritdoc />
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChannelTransport"/>
    /// </summary>
    public ChannelTransport(ILoggerProvider logger)
    {
        this.logger = logger;
        secureTransport = new TcpTransport(logger);
        fastTransport = new UdpTransport(logger);

        secureTransport.RawMessageReceived += (_, e) => RawMessageReceived?.Invoke(this, e);
        secureTransport.ConnectionStateChanged += OnSecureConnectionStateChanged;

        fastTransport.RawMessageReceived += (_, e) => RawMessageReceived?.Invoke(this, e);
    }

    private void OnSecureConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (!e.IsConnected)
        {
            _ = CleanupFastChannelAsync(e);
        }
        ConnectionStateChanged?.Invoke(this, e);
    }

    private async Task CleanupFastChannelAsync(ConnectionStateChangedEventArgs e)
    {
        try
        {
            await fastTransport.DisconnectClientAsync(e.ConnectionId, e.ServerConnectionId, e.DisconnectReason);
        }
        catch (Exception ex)
        {
            logger.Log($"Не удалось почистить udp-канал {e.ConnectionId}: {ex.Message}", LogLevel.Warning);
        }
    }

    async Task<Guid> ITransport.StartHostingAsync(Guid? serverConnectionId, ushort? prefferedPort, int? sequentialAttempts, CancellationToken cancellationToken)
    {
        // Один id сервера на комнату для обеих ног
        var connectionId = serverConnectionId ?? Guid.NewGuid();
        await secureTransport.StartHostingAsync(connectionId, prefferedPort, sequentialAttempts, cancellationToken);
        await fastTransport.StartHostingAsync(connectionId, cancellationToken);
        return connectionId;
    }

    async Task ITransport.StopHostingAsync(Guid connectionId)
    {
        await secureTransport.StopHostingAsync(connectionId);
        await fastTransport.StopHostingAsync(connectionId);
    }

    async Task<Guid> ITransport.ConnectAsync(IEndpoint endpoint, Guid? connectionId, CancellationToken cancellationToken)
    {
        if (endpoint is TcpEndpoint)
        {
            return await secureTransport.ConnectAsync(endpoint, connectionId, cancellationToken);
        }

        if (endpoint is UdpEndpoint)
        {
            return await fastTransport.ConnectAsync(endpoint, connectionId, cancellationToken);
        }

        throw new ArgumentException($"Неподдерживаемый endpoint: {endpoint.GetType().Name}");
    }

    async Task ITransport.SendAsync(byte[] data, Guid recipientConnectionId, Guid? serverConnectionId, MessageChannel channel, CancellationToken cancellationToken)
    {
        if (channel == MessageChannel.Fast && fastTransport.IsReadyToSend(recipientConnectionId, serverConnectionId))
        {
            try
            {
                await fastTransport.SendAsync(data, recipientConnectionId, serverConnectionId, MessageChannel.Fast, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.Log($"UDP отправка не удалась, падаю на TCP: {ex.Message}", LogLevel.Warning);
            }
        }

        await secureTransport.SendAsync(data, recipientConnectionId, serverConnectionId, MessageChannel.Secure, cancellationToken);
    }

    async Task ITransport.BroadcastAsync(byte[] data, Guid connectionId, IEnumerable<Guid>? excludeConnections, MessageChannel channel, CancellationToken cancellationToken)
    {
        if (channel == MessageChannel.Fast)
        {
            try
            {
                await fastTransport.BroadcastAsync(data, connectionId, excludeConnections, MessageChannel.Fast, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                logger.Log($"UDP broadcast не удался, падаю на TCP: {ex.Message}");
            }
        }

        await secureTransport.BroadcastAsync(data, connectionId, excludeConnections, MessageChannel.Secure, cancellationToken);
    }

    async Task<IEnumerable<IEndpoint>> ITransport.SetUpEndpoints(Guid connectionId, NetworkOptions networkOptions, NetworkOptions? oldNetworkOptions, CancellationToken cancellationToken)
    {
        var tcpEndpoints = await secureTransport.SetUpEndpoints(connectionId, networkOptions, oldNetworkOptions, cancellationToken);
        await fastTransport.SetUpEndpoints(connectionId, networkOptions, oldNetworkOptions, cancellationToken);
        // Возращаются только TCP endpoint потому что у них обрабатывается подключение
        return tcpEndpoints;
    }

    IEnumerable<IEndpoint> ITransport.GetEndpoints(Guid serverConnectionId)
    {
        var tcpEndpoints = secureTransport.GetEndpoints(serverConnectionId);
        var udpEndpoints = fastTransport.GetEndpoints(serverConnectionId);
        return tcpEndpoints.Concat(udpEndpoints);
    }

    async Task ITransport.DisconnectClientAsync(Guid clientConnectionId, Guid? serverConnectionId, DisconnectReason reason)
    {
        await secureTransport.DisconnectClientAsync(clientConnectionId, serverConnectionId, reason);
        await fastTransport.DisconnectClientAsync(clientConnectionId, serverConnectionId, reason);
    }

    async Task ITransport.DisconnectAsync(Guid connectionId, DisconnectReason reason)
    {
        await secureTransport.DisconnectAsync(connectionId, reason);
        await fastTransport.DisconnectAsync(connectionId, reason);
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public async ValueTask DisposeAsync()
    {
        await secureTransport.DisposeAsync();
        await fastTransport.DisposeAsync();
    }
}
