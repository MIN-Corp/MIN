using FluentAssertions;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Stores.Contracts.Exceptions;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Registries;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="RoomConnectionRegistry"/>
/// </summary>
public class RoomConnectionRegistryTests
{
    private readonly IRoomConnectionRegistry registry = new RoomConnectionRegistry();

    /// <summary>
    /// Серверная регистрация даёт роль Host и двусторонние связи
    /// </summary>
    [Fact]
    public void RegisterServerConnectionShouldGiveHostRole()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var serverConnectionId = Guid.NewGuid();

        // Act
        registry.RegisterServerConnection(roomId, serverConnectionId);

        // Assert
        registry.GetRole(roomId).Should().Be(Role.Host);
        registry.IsHosting(roomId).Should().BeTrue();
        registry.IsConnected(roomId).Should().BeFalse();
        registry.GetServerConnectionIdByRoomId(roomId).Should().Be(serverConnectionId);
        registry.GetRoomIdByServerConnectionId(serverConnectionId).Should().Be(roomId);
        registry.GetServerConnectionCount().Should().Be(1);
    }

    /// <summary>
    /// Клиентская регистрация даёт роль Client
    /// </summary>
    [Fact]
    public void RegisterClientConnectionShouldGiveClientRole()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        // Act
        registry.RegisterClientConnection(roomId, connectionId);

        // Assert
        registry.GetRole(roomId).Should().Be(Role.Client);
        registry.IsConnected(roomId).Should().BeTrue();
        registry.GetClientConnectionIdByRoomId(roomId).Should().Be(connectionId);
        registry.GetRoomIdByClientConnectionId(connectionId).Should().Be(roomId);
        registry.GetClientConnectionCount().Should().Be(1);
    }

    /// <summary>
    /// Неизвестная комната бросает RoomNotRegisteredException
    /// </summary>
    [Fact]
    public void UnknownRoomShouldThrow()
    {
        // Act
        var getRole = () => registry.GetRole(Guid.NewGuid());
        var getServer = () => registry.GetServerConnectionIdByRoomId(Guid.NewGuid());
        var getClient = () => registry.GetClientConnectionIdByRoomId(Guid.NewGuid());

        // Assert
        getRole.Should().Throw<RoomNotRegisteredException>();
        getServer.Should().Throw<RoomNotRegisteredException>();
        getClient.Should().Throw<RoomNotRegisteredException>();
    }

    /// <summary>
    /// Detach помечает серверное соединение офлайн, роль остаётся Host
    /// </summary>
    [Fact]
    public void DetachServerConnectionShouldKeepRoleButDropConnection()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var serverConnectionId = Guid.NewGuid();
        registry.RegisterServerConnection(roomId, serverConnectionId);

        // Act
        registry.DetachServerConnection(roomId);

        // Assert
        registry.GetRole(roomId).Should().Be(Role.Host);
        registry.GetServerConnectionCount().Should().Be(0);
        registry.TryGetServerConnectionIdByRoomId(roomId, out _).Should().BeFalse();
        registry.TryGetRoomIdByServerConnectionId(serverConnectionId, out _).Should().BeFalse();
    }

    /// <summary>
    /// Отрегистрация серверного соединения убирает комнату
    /// </summary>
    [Fact]
    public void UnregisterServerConnectionShouldRemoveRoom()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        registry.RegisterServerConnection(roomId, Guid.NewGuid());

        // Act
        registry.UnregisterServerConnection(roomId);

        // Assert
        registry.IsHosting(roomId).Should().BeFalse();
        registry.GetServerConnectionCount().Should().Be(0);
    }

    /// <summary>
    /// Отрегистрация клиентского соединения убирает комнату
    /// </summary>
    [Fact]
    public void UnregisterClientConnectionShouldRemoveRoom()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        registry.RegisterClientConnection(roomId, connectionId);

        // Act
        registry.UnregisterClientConnection(connectionId);

        // Assert
        registry.IsConnected(roomId).Should().BeFalse();
        registry.GetClientConnectionCount().Should().Be(0);
        registry.TryGetRoomIdByClientConnectionId(connectionId, out _).Should().BeFalse();
    }

    /// <summary>
    /// TryGet с null-аргументами возвращает false
    /// </summary>
    [Fact]
    public void TryGetWithNullShouldReturnFalse()
    {
        // Act
        var server = registry.TryGetServerConnectionIdByRoomId(null, out _);
        var client = registry.TryGetClientConnectionIdByRoomId(null, out _);
        var roomByServer = registry.TryGetRoomIdByServerConnectionId(null, out _);
        var roomByClient = registry.TryGetRoomIdByClientConnectionId(null, out _);

        // Assert
        server.Should().BeFalse();
        client.Should().BeFalse();
        roomByServer.Should().BeFalse();
        roomByClient.Should().BeFalse();
    }

    /// <summary>
    /// Регистрация серверного соединения без id (оффлайн-комната) даёт роль Host без соединения
    /// </summary>
    [Fact]
    public void RegisterServerConnectionWithoutIdShouldGiveHostRole()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act
        registry.RegisterServerConnection(roomId, null);

        // Assert
        registry.GetRole(roomId).Should().Be(Role.Host);
        registry.GetServerConnectionCount().Should().Be(0);
        registry.TryGetServerConnectionIdByRoomId(roomId, out _).Should().BeFalse();
    }
}
