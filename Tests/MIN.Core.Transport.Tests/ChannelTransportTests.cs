using System.Collections.Concurrent;
using FluentAssertions;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Core.Transport.UdpSockets.Models;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Transport.Tests;

/// <summary>
/// Интеграционные тесты <see cref="ChannelTransport"/>
/// </summary>
public class ChannelTransportTests
{
    private readonly TestLoggerProvider logger = new();

    /// <summary>
    /// Неизвестный тип endpoint отклоняется
    /// </summary>
    private sealed class FakeEndpoint : IEndpoint
    {
        public TransportType Type => TransportType.Tcp;
        public AddressOrigin Origin => AddressOrigin.Lan;
        public string GetOrigin() => "Fake";
        public string GetAddress() => "fake:0";
    }

    /// <summary>
    /// Хостинг поднимает TCP и UDP на одном порту, SetUpEndpoints возвращает оба типа
    /// </summary>
    [Fact]
    public async Task StartHostingShouldSetUpBothChannels()
    {
        // Arrange
        await using var channel = new ChannelTransport(logger);
        ITransport transport = channel;
        var port = TestPorts.GetFreeTcpPort();

        // Act
        var hostingId = await transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var endpoints = (await transport.SetUpEndpoints(hostingId, new Contracts.Models.NetworkOptions(), null, CancellationToken.None)).ToList();
        var all = transport.GetEndpoints(hostingId).ToList();

        // Assert
        endpoints.Should().OnlyContain(e => e.Type == TransportType.Tcp);
        all.Should().Contain(e => e.Type == TransportType.Tcp);
        all.Should().Contain(e => e.Type == TransportType.Udp);
        all.Should().OnlyContain(e => (e.Type == TransportType.Tcp ? ((TcpEndpoint)e).Port : ((UdpEndpoint)e).Port) == port);
    }

    /// <summary>
    /// Fast-канал без готового UDP падает обратно на TCP
    /// </summary>
    [Fact]
    public async Task FastChannelShouldFallBackToTcp()
    {
        // Arrange
        await using var serverChannel = new ChannelTransport(logger);
        ITransport server = serverChannel;
        await using var clientChannel = new ChannelTransport(logger);
        ITransport client = clientChannel;
        var serverReceived = new ConcurrentQueue<RawMessageReceivedEventArgs>();
        server.RawMessageReceived += (_, e) => serverReceived.Enqueue(e);
        var port = TestPorts.GetFreeTcpPort();
        await server.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await Task.Delay(300);

        // Act
        await client.SendAsync(new byte[] { 5 }, clientId, null, MessageChannel.Fast, CancellationToken.None);
        await TestWait.UntilAsync(() => !serverReceived.IsEmpty);

        // Assert
        serverReceived.Single().Data.Should().Equal([5]);
    }

    /// <summary>
    /// Подключение по TCP-endpoint устанавливает secure-канал
    /// </summary>
    [Fact]
    public async Task ConnectViaTcpEndpointShouldWork()
    {
        // Arrange
        await using var serverChannel = new ChannelTransport(logger);
        ITransport server = serverChannel;
        await using var clientChannel = new ChannelTransport(logger);
        ITransport client = clientChannel;
        var serverReceived = new ConcurrentQueue<RawMessageReceivedEventArgs>();
        var clientReceived = new ConcurrentQueue<RawMessageReceivedEventArgs>();
        server.RawMessageReceived += (_, e) => serverReceived.Enqueue(e);
        client.RawMessageReceived += (_, e) => clientReceived.Enqueue(e);
        var port = TestPorts.GetFreeTcpPort();
        var hostingId = await server.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();

        // Act
        var connectedId = await client.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await Task.Delay(300);
        await client.SendAsync(new byte[] { 1 }, connectedId, null, MessageChannel.Secure, CancellationToken.None);
        await TestWait.UntilAsync(() => !serverReceived.IsEmpty);
        await server.SendAsync(new byte[] { 2 }, clientId, hostingId, MessageChannel.Secure, CancellationToken.None);
        await TestWait.UntilAsync(() => !clientReceived.IsEmpty);

        // Assert
        connectedId.Should().Be(clientId);
        serverReceived.Single().Data.Should().Equal([1]);
        clientReceived.Single().Data.Should().Equal([2]);
    }

    /// <summary>
    /// Неподдерживаемый endpoint отклоняется
    /// </summary>
    [Fact]
    public async Task ConnectWithUnsupportedEndpointShouldThrow()
    {
        // Arrange
        await using var channel = new ChannelTransport(logger);
        ITransport transport = channel;

        // Act
        var act = () => transport.ConnectAsync(new FakeEndpoint(), null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Broadcast по secure-каналу доходит до клиентов
    /// </summary>
    [Fact]
    public async Task BroadcastShouldReachClients()
    {
        // Arrange
        await using var serverChannel = new ChannelTransport(logger);
        ITransport server = serverChannel;
        await using var clientChannel = new ChannelTransport(logger);
        ITransport client = clientChannel;
        var clientReceived = new ConcurrentQueue<byte[]>();
        client.RawMessageReceived += (_, e) => clientReceived.Enqueue(e.Data);
        var port = TestPorts.GetFreeTcpPort();
        var hostingId = await server.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
        var clientId = Guid.NewGuid();
        await client.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
        await Task.Delay(300);

        // Act
        await server.BroadcastAsync(new byte[] { 3 }, hostingId, null, MessageChannel.Secure, CancellationToken.None);
        await TestWait.UntilAsync(() => !clientReceived.IsEmpty);

        // Assert
        clientReceived.Single().Should().Equal([3]);
    }
}
