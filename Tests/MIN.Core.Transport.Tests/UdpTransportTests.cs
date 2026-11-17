using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.UdpSockets;
using MIN.Core.Transport.UdpSockets.Models;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Transport.Tests;

/// <summary>
/// Интеграционные тесты <see cref="UdpTransport"/> на loopback
/// </summary>
public class UdpTransportTests
{
    private readonly TestLoggerProvider logger = new();

    /// <summary>
    /// Байты доходят от клиента к серверу и обратно
    /// </summary>
    [Fact]
    public async Task SendShouldDeliverBytesBothDirections()
    {
        // Arrange
        await using var server = new UdpTransport(logger);
        await using var client = new UdpTransport(logger);
        var serverReceived = new ConcurrentQueue<RawMessageReceivedEventArgs>();
        var clientReceived = new ConcurrentQueue<RawMessageReceivedEventArgs>();
        server.RawMessageReceived += (_, e) => serverReceived.Enqueue(e);
        client.RawMessageReceived += (_, e) => clientReceived.Enqueue(e);
        var port = TestPorts.GetFreeUdpPort();
        var hostingId = await server.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.ConnectAsync(new UdpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.IsReadyToSend(clientId, hostingId));

        // Act
        await client.SendAsync(new byte[] { 1, 2, 3 }, clientId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => serverReceived.Count >= 1);
        await server.SendAsync(new byte[] { 9, 8 }, clientId, hostingId, CancellationToken.None);
        await TestWait.UntilAsync(() => clientReceived.Count >= 1);

        // Assert
        var got = serverReceived.Single();
        got.Data.Should().Equal([1, 2, 3]);
        got.ConnectionId.Should().Be(clientId);
        got.ServerConnectionId.Should().Be(hostingId);
        clientReceived.Single().Data.Should().Equal([9, 8]);
    }

    /// <summary>
    /// Мусорная датаграмма не убивает приём последующих валидных пакетов
    /// </summary>
    [Fact]
    public async Task GarbageDatagramShouldNotKillReceiveLoop()
    {
        // Arrange
        await using var server = new UdpTransport(logger);
        await using var client = new UdpTransport(logger);
        var serverReceived = new ConcurrentQueue<RawMessageReceivedEventArgs>();
        server.RawMessageReceived += (_, e) => serverReceived.Enqueue(e);
        var port = TestPorts.GetFreeUdpPort();
        var hostingId = await server.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.ConnectAsync(new UdpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.IsReadyToSend(clientId, hostingId));
        using var garbageSender = new UdpClient();
        await garbageSender.SendAsync(new byte[] { 1, 2, 3 }, 3, new IPEndPoint(IPAddress.Loopback, port));
        await Task.Delay(100);

        // Act
        await client.SendAsync(new byte[] { 42 }, clientId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => serverReceived.Any(e => e.Data.SequenceEqual(new byte[] { 42 })), timeoutMs: 3000);

        // Assert
        serverReceived.Should().Contain(e => e.Data.SequenceEqual(new byte[] { 42 }));
    }

    /// <summary>
    /// IsReadyToSend отражает наличие соединения
    /// </summary>
    [Fact]
    public async Task IsReadyToSendShouldReflectConnections()
    {
        // Arrange
        await using var server = new UdpTransport(logger);
        await using var client = new UdpTransport(logger);
        var port = TestPorts.GetFreeUdpPort();
        var hostingId = await server.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();

        // Act
        var before = server.IsReadyToSend(clientId, hostingId);
        await client.ConnectAsync(new UdpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.IsReadyToSend(clientId, hostingId));
        var clientReady = client.IsReadyToSend(clientId, null);

        // Assert
        before.Should().BeFalse();
        server.IsReadyToSend(clientId, hostingId).Should().BeTrue();
        clientReady.Should().BeTrue();
    }

    /// <summary>
    /// Broadcast доходит до всех кроме исключённых
    /// </summary>
    [Fact]
    public async Task BroadcastShouldExcludeConnections()
    {
        // Arrange
        await using var server = new UdpTransport(logger);
        await using var client1 = new UdpTransport(logger);
        await using var client2 = new UdpTransport(logger);
        var received1 = new ConcurrentQueue<byte[]>();
        var received2 = new ConcurrentQueue<byte[]>();
        client1.RawMessageReceived += (_, e) => received1.Enqueue(e.Data);
        client2.RawMessageReceived += (_, e) => received2.Enqueue(e.Data);
        var port = TestPorts.GetFreeUdpPort();
        var hostingId = await server.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await client1.ConnectAsync(new UdpEndpoint { IpAddress = "127.0.0.1", Port = port }, id1, CancellationToken.None);
        await client2.ConnectAsync(new UdpEndpoint { IpAddress = "127.0.0.1", Port = port }, id2, CancellationToken.None);
        await TestWait.UntilAsync(() => server.IsReadyToSend(id1, hostingId) && server.IsReadyToSend(id2, hostingId));

        // Act
        await server.BroadcastAsync(new byte[] { 7 }, hostingId, [id1], CancellationToken.None);
        await TestWait.UntilAsync(() => received2.Count >= 1);
        await Task.Delay(200);

        // Assert
        received2.Single().Should().Equal([7]);
        received1.Should().BeEmpty();
    }

    /// <summary>
    /// GetEndpoints после StartHosting без SetUpEndpoints бросает InvalidOperationException
    /// </summary>
    [Fact]
    public async Task GetEndpointsWithoutSetUpShouldThrowInvalidOperation()
    {
        // Arrange
        await using var transport = new UdpTransport(logger);
        var port = TestPorts.GetFreeUdpPort();
        var hostingId = await transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);

        // Act
        var act = () => transport.GetEndpoints(hostingId);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Подключение с неверным типом endpoint отклоняется
    /// </summary>
    [Fact]
    public async Task ConnectWithWrongEndpointTypeShouldThrow()
    {
        // Arrange
        await using var transport = new UdpTransport(logger);

        // Act
        var act = () => transport.ConnectAsync(new TcpSockets.Models.TcpEndpoint { IpAddress = "127.0.0.1", Port = 1 }, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Отключение клиента убирает соединение из сервера
    /// </summary>
    [Fact]
    public async Task DisconnectClientShouldRemoveConnection()
    {
        // Arrange
        await using var server = new UdpTransport(logger);
        await using var client = new UdpTransport(logger);
        var port = TestPorts.GetFreeUdpPort();
        var hostingId = await server.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.ConnectAsync(new UdpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await TestWait.UntilAsync(() => server.IsReadyToSend(clientId, hostingId));

        // Act
        await server.DisconnectClientAsync(clientId, hostingId, DisconnectReason.Kick);

        // Assert
        server.IsReadyToSend(clientId, hostingId).Should().BeFalse();
    }
}
