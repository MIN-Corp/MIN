using System.Text;
using System.Text.Json;
using FluentAssertions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Protocol.Contracts.Constants;
using MIN.Core.Protocol.Contracts.Interfaces;
using MIN.Core.Protocol.Services;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Protocol.Tests;

/// <summary>
/// Тесты на <see cref="HostHandshakeService"/> и <see cref="ClientHandshakeService"/>
/// </summary>
public class HandshakeServiceTests
{
    private readonly TestLoggerProvider logger = new();

    /// <summary>
    /// Валидный запрос клиента получает ответ с информацией о комнате
    /// </summary>
    [Fact]
    public async Task HostValidRequestShouldRespondWithRoomInfo()
    {
        // Arrange
        var transport = new InMemoryTransport();
        IHostHandshake host = new HostHandshakeService(transport, logger);
        var serverConnectionId = Guid.NewGuid();
        var clientConnectionId = Guid.NewGuid();
        var roomInfo = new RoomInfo { Name = "Комната" };

        // Act
        var task = host.HandleServerAsync(serverConnectionId, clientConnectionId, roomInfo, CancellationToken.None);
        transport.Deliver(Encoding.UTF8.GetBytes(ProtocolConstants.ResponseStarter), clientConnectionId, serverConnectionId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var sent = transport.SentDatagrams.Single();
        var responseText = Encoding.UTF8.GetString(sent.Data);
        responseText.Should().StartWith(ProtocolConstants.ResponseStarter);
        var sentRoom = JsonSerializer.Deserialize<RoomInfo>(responseText[ProtocolConstants.ResponseStarter.Length..]);
        sentRoom!.Id.Should().Be(roomInfo.Id);
        sent.RecipientConnectionId.Should().Be(clientConnectionId);
    }

    /// <summary>
    /// Неверный протокол отклоняется
    /// </summary>
    [Fact]
    public async Task HostWrongProtocolShouldFail()
    {
        // Arrange
        var transport = new InMemoryTransport();
        IHostHandshake host = new HostHandshakeService(transport, logger);
        var clientConnectionId = Guid.NewGuid();

        // Act
        var task = host.HandleServerAsync(Guid.NewGuid(), clientConnectionId, new RoomInfo(), CancellationToken.None);
        transport.Deliver(Encoding.UTF8.GetBytes("GARBAGE"), clientConnectionId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Неверный протокол");
        transport.SentDatagrams.Should().BeEmpty();
    }

    /// <summary>
    /// Сообщения от других соединений игнорируются
    /// </summary>
    [Fact]
    public async Task HostForeignConnectionShouldBeIgnored()
    {
        // Arrange
        var transport = new InMemoryTransport();
        IHostHandshake host = new HostHandshakeService(transport, logger);
        var clientConnectionId = Guid.NewGuid();

        // Act
        var task = host.HandleServerAsync(Guid.NewGuid(), clientConnectionId, new RoomInfo(), CancellationToken.None);
        transport.Deliver(Encoding.UTF8.GetBytes(ProtocolConstants.ResponseStarter), Guid.NewGuid());
        await Task.Delay(100);
        task.IsCompleted.Should().BeFalse();
        transport.Deliver(Encoding.UTF8.GetBytes(ProtocolConstants.ResponseStarter), clientConnectionId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Валидный ответ сервера возвращает информацию о комнате
    /// </summary>
    [Fact]
    public async Task ClientValidResponseShouldReturnRoomInfo()
    {
        // Arrange
        var transport = new InMemoryTransport();
        IClientHandshake client = new ClientHandshakeService(transport, logger);
        var connectionId = Guid.NewGuid();
        var roomInfo = new RoomInfo { Name = "Комната" };
        var response = Encoding.UTF8.GetBytes(ProtocolConstants.ResponseStarter + JsonSerializer.Serialize(roomInfo));
        transport.SendHandler = datagram =>
        {
            transport.Deliver(response, connectionId);
            return Task.CompletedTask;
        };

        // Act
        var result = await client.HandleClientAsync(connectionId, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RoomInfo.Id.Should().Be(roomInfo.Id);
        result.RoomInfo.Name.Should().Be("Комната");
    }

    /// <summary>
    /// Ответ не по протоколу MIN отклоняется
    /// </summary>
    [Fact]
    public async Task ClientWrongProtocolShouldFail()
    {
        // Arrange
        var transport = new InMemoryTransport();
        IClientHandshake client = new ClientHandshakeService(transport, logger);
        var connectionId = Guid.NewGuid();
        transport.SendHandler = datagram =>
        {
            transport.Deliver(Encoding.UTF8.GetBytes("NOT MIN PROTOCOL"), connectionId);
            return Task.CompletedTask;
        };

        // Act
        var result = await client.HandleClientAsync(connectionId, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("не соответсвует MIN протоколу");
    }

    /// <summary>
    /// Null вместо RoomInfo отклоняется
    /// </summary>
    [Fact]
    public async Task ClientNullRoomInfoShouldFail()
    {
        // Arrange
        var transport = new InMemoryTransport();
        IClientHandshake client = new ClientHandshakeService(transport, logger);
        var connectionId = Guid.NewGuid();
        transport.SendHandler = datagram =>
        {
            transport.Deliver(Encoding.UTF8.GetBytes(ProtocolConstants.ResponseStarter + "null"), connectionId);
            return Task.CompletedTask;
        };

        // Act
        var result = await client.HandleClientAsync(connectionId, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("десериализовать");
    }

    /// <summary>
    /// Разрыв соединения сервером проваливает рукопожатие
    /// </summary>
    [Fact]
    public async Task ClientDisconnectShouldFail()
    {
        // Arrange
        var transport = new InMemoryTransport();
        IClientHandshake client = new ClientHandshakeService(transport, logger);
        var connectionId = Guid.NewGuid();
        transport.SendHandler = datagram =>
        {
            transport.RaiseConnectionStateChanged(new ConnectionStateChangedEventArgs(connectionId, false, DisconnectReason.Error));
            return Task.CompletedTask;
        };

        // Act
        var result = await client.HandleClientAsync(connectionId, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("разорвал");
    }
}
