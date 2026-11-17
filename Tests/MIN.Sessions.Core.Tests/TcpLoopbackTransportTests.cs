using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using MIN.Sessions.Core.Transport.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Models;
using MIN.Sessions.Core.Transport.Tcp;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Sessions.Core.Tests;

/// <summary>
/// Интеграционные тесты <see cref="TcpLoopbackTransport"/>
/// </summary>
public class TcpLoopbackTransportTests
{
    private static readonly SessionInstanceContext Context = new(Guid.NewGuid(), 1, default);

    private static int GetPort(ISessionProcessTransport transport)
    {
        var json = JsonDocument.Parse(transport.GetConnectionString());
        var value = json.RootElement.GetProperty("Value").GetString()!;
        return int.Parse(value.Split(':')[1]);
    }

    /// <summary>
    /// Подключение процесса устанавливается и байты доходят в обе стороны
    /// </summary>
    [Fact]
    public async Task ShouldTransferBytesBothDirections()
    {
        // Arrange
        await using var loopback = new TcpLoopbackTransport();
        var transport = (ISessionProcessTransport)loopback;
        await transport.StartAsync(Guid.NewGuid(), CancellationToken.None);
        var port = GetPort(transport);
        using var process = new TcpClient();
        await process.ConnectAsync("127.0.0.1", port);
        var received = new TaskCompletionSource<byte[]>();
        transport.MessageReceived += (_, e) => received.TrySetResult(e.Data);

        // Act
        await transport.WaitForConnectionAsync(Context, 5000, CancellationToken.None);
        var stream = process.GetStream();
        var payload = new byte[] { 1, 2, 3 };
        await stream.WriteAsync(BitConverter.GetBytes(payload.Length));
        await stream.WriteAsync(payload);
        var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await transport.SendAsync(new byte[] { 9, 8 }, Context, CancellationToken.None);
        var lengthBuf = new byte[4];
        await stream.ReadExactlyAsync(lengthBuf);
        var body = new byte[BitConverter.ToInt32(lengthBuf)];
        await stream.ReadExactlyAsync(body);

        // Assert
        transport.IsConnectionExists(Context).Should().BeTrue();
        got.Should().Equal(payload);
        body.Should().Equal([9, 8]);
    }

    /// <summary>
    /// Таймаут ожидания подключения не роняет транспорт
    /// </summary>
    [Fact]
    public async Task WaitTimeoutShouldNotThrow()
    {
        // Arrange
        await using var loopback = new TcpLoopbackTransport();
        var transport = (ISessionProcessTransport)loopback;
        await transport.StartAsync(Guid.NewGuid(), CancellationToken.None);

        // Act
        await transport.WaitForConnectionAsync(Context, 200, CancellationToken.None);

        // Assert
        transport.IsConnectionExists(Context).Should().BeFalse();
    }

    /// <summary>
    /// Отключение убирает соединение
    /// </summary>
    [Fact]
    public async Task DisconnectShouldRemoveConnection()
    {
        // Arrange
        await using var loopback = new TcpLoopbackTransport();
        var transport = (ISessionProcessTransport)loopback;
        await transport.StartAsync(Guid.NewGuid(), CancellationToken.None);
        using var process = new TcpClient();
        await process.ConnectAsync("127.0.0.1", GetPort(transport));
        await transport.WaitForConnectionAsync(Context, 5000, CancellationToken.None);

        // Act
        await transport.DisconnectAsync(Context);

        // Assert
        transport.IsConnectionExists(Context).Should().BeFalse();
    }

    /// <summary>
    /// Строка подключения содержит тип tcp и loopback-адрес
    /// </summary>
    [Fact]
    public async Task ConnectionStringShouldBeTcpLoopback()
    {
        // Arrange
        await using var loopback = new TcpLoopbackTransport();
        var transport = (ISessionProcessTransport)loopback;
        await transport.StartAsync(Guid.NewGuid(), CancellationToken.None);

        // Act
        var json = JsonDocument.Parse(transport.GetConnectionString());

        // Assert
        json.RootElement.GetProperty("Type").GetString().Should().Be("tcp");
        json.RootElement.GetProperty("Value").GetString().Should().StartWith("127.0.0.1:");
    }
}
