using System.Collections.Concurrent;
using System.Net.Sockets;
using FluentAssertions;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Helpers;
using MIN.Core.Transport.TcpSockets;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Transport.Tests;

/// <summary>
/// Интеграционные тесты <see cref="TcpTransport"/> на loopback
/// </summary>
public class TcpTransportTests
{
    private readonly TestLoggerProvider logger = new();

    /// <summary>
    /// Узел: транспорт + очередь полученных сообщений + события соединений
    /// </summary>
    private sealed class Node : IAsyncDisposable
    {
        public TcpTransport Transport { get; }
        public ConcurrentQueue<RawMessageReceivedEventArgs> Received { get; } = new();
        public ConcurrentQueue<ConnectionStateChangedEventArgs> StateChanges { get; } = new();

        public Node(TestLoggerProvider logger)
        {
            Transport = new TcpTransport(logger);
            Transport.RawMessageReceived += (_, e) => Received.Enqueue(e);
            Transport.ConnectionStateChanged += (_, e) => StateChanges.Enqueue(e);
        }

        public async ValueTask DisposeAsync() => await Transport.DisposeAsync();
    }

    /// <summary>
    /// Байты доходят от клиента к серверу и обратно без искажений
    /// </summary>
    [Fact]
    public async Task SendShouldDeliverBytesBothDirections()
    {
        // Arrange
        await using var server = new Node(logger);
        await using var client = new Node(logger);
        var port = TestPorts.GetFreeTcpPort();
        var hostingId = await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.StateChanges.Any(e => e.IsConnected && e.ConnectionId == clientId));
        var toServer = new byte[] { 1, 2, 3, 4, 5 };
        var toClient = new byte[] { 9, 8, 7 };

        // Act
        await client.Transport.SendAsync(toServer, clientId, null, MessageChannel.Secure, CancellationToken.None);
        await TestWait.UntilAsync(() => server.Received.Count >= 1);
        await server.Transport.SendAsync(toClient, clientId, hostingId, MessageChannel.Secure, CancellationToken.None);
        await TestWait.UntilAsync(() => client.Received.Count >= 1);

        // Assert
        var serverGot = server.Received.Single();
        serverGot.Data.Should().Equal(toServer);
        serverGot.ConnectionId.Should().Be(clientId);
        serverGot.ServerConnectionId.Should().Be(hostingId);
        client.Received.Single().Data.Should().Equal(toClient);
    }

    /// <summary>
    /// Большое сообщение (1 МиБ) доходит целиком
    /// </summary>
    [Fact]
    public async Task LargeMessageShouldArriveIntact()
    {
        // Arrange
        await using var server = new Node(logger);
        await using var client = new Node(logger);
        var port = TestPorts.GetFreeTcpPort();
        await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.StateChanges.Any(e => e.IsConnected && e.ConnectionId == clientId));
        var payload = new byte[1024 * 1024];
        Random.Shared.NextBytes(payload);

        // Act
        await client.Transport.SendAsync(payload, clientId, null, MessageChannel.Secure, CancellationToken.None);
        await TestWait.UntilAsync(() => server.Received.Count >= 1, timeoutMs: 10000);

        // Assert
        server.Received.Single().Data.Should().Equal(payload);
    }

    /// <summary>
    /// Сообщения приходят в порядке отправки
    /// </summary>
    [Fact]
    public async Task MessagesShouldArriveInOrder()
    {
        // Arrange
        await using var server = new Node(logger);
        await using var client = new Node(logger);
        var port = TestPorts.GetFreeTcpPort();
        await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.StateChanges.Any(e => e.IsConnected && e.ConnectionId == clientId));

        // Act
        for (var i = 0; i < 100; i++)
        {
            await client.Transport.SendAsync(BitConverter.GetBytes(i), clientId, null, MessageChannel.Secure, CancellationToken.None);
        }

        await TestWait.UntilAsync(() => server.Received.Count >= 100);

        // Assert
        server.Received.Select(e => BitConverter.ToInt32(e.Data)).Should().Equal(Enumerable.Range(0, 100));
    }

    /// <summary>
    /// Broadcast доходит до всех клиентов кроме исключённых
    /// </summary>
    [Fact]
    public async Task BroadcastShouldExcludeConnections()
    {
        // Arrange
        await using var server = new Node(logger);
        await using var client1 = new Node(logger);
        await using var client2 = new Node(logger);
        var port = TestPorts.GetFreeTcpPort();
        var hostingId = await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await client1.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, id1, CancellationToken.None);
        await client2.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, id2, CancellationToken.None);
        await TestWait.UntilAsync(() => server.StateChanges.Count(e => e.IsConnected) >= 2);

        // Act
        await server.Transport.BroadcastAsync(new byte[] { 42 }, hostingId, [id1], MessageChannel.Secure, CancellationToken.None);
        await TestWait.UntilAsync(() => client2.Received.Count >= 1);
        await Task.Delay(200);

        // Assert
        client2.Received.Single().Data.Should().Equal([42]);
        client1.Received.Should().BeEmpty();
    }

    /// <summary>
    /// Отправка неизвестному получателю молча игнорируется
    /// </summary>
    [Fact]
    public async Task SendToUnknownRecipientShouldBeIgnored()
    {
        // Arrange
        await using var transport = new TcpTransport(logger);

        // Act
        var act = () => transport.SendAsync(new byte[] { 1 }, Guid.NewGuid(), null, MessageChannel.Secure, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// GetEndpoints после StartHosting без SetUpEndpoints бросает InvalidOperationException
    /// </summary>
    [Fact]
    public async Task GetEndpointsWithoutSetUpShouldThrowInvalidOperation()
    {
        // Arrange
        await using var transport = new TcpTransport(logger);
        var port = TestPorts.GetFreeTcpPort();
        var hostingId = await transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);

        // Act
        var act = () => transport.GetEndpoints(hostingId);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// GetEndpoints для нехостируемого соединения бросает InvalidOperationException
    /// </summary>
    [Fact]
    public void GetEndpointsNotHostedShouldThrow()
    {
        // Arrange
        var transport = new TcpTransport(logger);

        // Act
        var act = () => transport.GetEndpoints(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// SetUpEndpoints без проброски порта возвращает LAN-адреса с портом сервера
    /// </summary>
    [Fact]
    public async Task SetUpEndpointsShouldReturnLanEndpoints()
    {
        // Arrange
        await using var transport = new TcpTransport(logger);
        var port = TestPorts.GetFreeTcpPort();
        var hostingId = await transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);

        // Act
        var endpoints = (await transport.SetUpEndpoints(hostingId, new Contracts.Models.NetworkOptions(), null, CancellationToken.None)).ToList();
        var cached = transport.GetEndpoints(hostingId).ToList();

        // Assert
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllBeOfType<TcpEndpoint>();
        endpoints.Should().OnlyContain(e => e.Type == TransportType.Tcp);
        cached.Should().BeEquivalentTo(endpoints);
    }

    /// <summary>
    /// Клиент с невалидной преамбулой отклоняется сервером
    /// </summary>
    [Fact]
    public async Task InvalidPreambleShouldBeRejected()
    {
        // Arrange
        await using var server = new Node(logger);
        var port = TestPorts.GetFreeTcpPort();
        await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        using var raw = new TcpClient();
        await raw.ConnectAsync("127.0.0.1", port);

        // Act
        var garbage = new byte[20];
        var stream = raw.GetStream();
        await stream.WriteAsync(BitConverter.GetBytes(garbage.Length));
        await stream.WriteAsync(garbage);
        var closed = await WaitEofAsync(stream);

        // Assert
        closed.Should().BeTrue();
        server.StateChanges.Should().NotContain(e => e.IsConnected);
    }

    /// <summary>
    /// Повторный идентификатор соединения отклоняется
    /// </summary>
    [Fact]
    public async Task DuplicateConnectionIdShouldBeRejected()
    {
        // Arrange
        await using var server = new Node(logger);
        await using var client = new Node(logger);
        var port = TestPorts.GetFreeTcpPort();
        await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.StateChanges.Any(e => e.IsConnected && e.ConnectionId == clientId));
        using var raw = new TcpClient();
        await raw.ConnectAsync("127.0.0.1", port);

        // Act
        var stream = raw.GetStream();
        var preamble = ConnectionPreamble.Create(clientId);
        await stream.WriteAsync(BitConverter.GetBytes(preamble.Length));
        await stream.WriteAsync(preamble);
        var closed = await WaitEofAsync(stream);

        // Assert
        closed.Should().BeTrue();
        server.StateChanges.Count(e => e.IsConnected).Should().Be(1);
    }

    /// <summary>
    /// Отрицательная длина кадра не роняет сервер
    /// </summary>
    [Fact]
    public async Task NegativeFrameLengthShouldNotCrashServer()
    {
        // Arrange
        await using var server = new Node(logger);
        await using var client = new Node(logger);
        var port = TestPorts.GetFreeTcpPort();
        await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        using var raw = new TcpClient();
        await raw.ConnectAsync("127.0.0.1", port);
        var stream = raw.GetStream();
        await stream.WriteAsync(BitConverter.GetBytes(-1));
        await WaitEofAsync(stream);

        // Act
        var clientId = Guid.NewGuid();
        await client.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.StateChanges.Any(e => e.IsConnected && e.ConnectionId == clientId));
        await client.Transport.SendAsync(new byte[] { 1 }, clientId, null, MessageChannel.Secure, CancellationToken.None);
        await TestWait.UntilAsync(() => !server.Received.IsEmpty);

        // Assert
        server.Received.Single().Data.Should().Equal([1]);
    }

    /// <summary>
    /// Отключение клиента порождает события на обеих сторонах
    /// </summary>
    [Fact]
    public async Task DisconnectShouldNotifyBothSides()
    {
        // Arrange
        await using var server = new Node(logger);
        await using var client = new Node(logger);
        var port = TestPorts.GetFreeTcpPort();
        var hostingId = await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.StateChanges.Any(e => e.IsConnected && e.ConnectionId == clientId));

        // Act
        await server.Transport.DisconnectClientAsync(clientId, hostingId, DisconnectReason.Kick);
        await TestWait.UntilAsync(() => client.StateChanges.Any(e => !e.IsConnected));

        // Assert
        server.StateChanges.Should().Contain(e => !e.IsConnected && e.ConnectionId == clientId);
        client.StateChanges.Should().Contain(e => !e.IsConnected);
    }

    /// <summary>
    /// Подключение с неверным типом endpoint отклоняется
    /// </summary>
    [Fact]
    public async Task ConnectWithWrongEndpointTypeShouldThrow()
    {
        // Arrange
        await using var transport = new TcpTransport(logger);

        // Act
        var act = () => transport.ConnectAsync(new UdpSockets.Models.UdpEndpoint { IpAddress = "127.0.0.1", Port = 1 }, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static async Task<bool> WaitEofAsync(NetworkStream stream)
    {
        try
        {
            var buffer = new byte[1];
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var read = await stream.ReadAsync(buffer, cts.Token);
            return read == 0;
        }
        catch (IOException)
        {
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
