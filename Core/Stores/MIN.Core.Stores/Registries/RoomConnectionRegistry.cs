using System.Collections.Concurrent;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Stores.Contracts.Exceptions;
using MIN.Core.Stores.Contracts.Registries.Interfaces;

namespace MIN.Core.Stores.Registries;

/// <inheritdoc cref="IRoomConnectionRegistry"/>
public class RoomConnectionRegistry : IRoomConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, Guid?> hostedRooms = new();            // RoomId -> ServerConnectionId
    private readonly ConcurrentDictionary<Guid, Guid> roomsByServerConnection = new(); // ServerConnectionId -> RoomId
    private readonly ConcurrentDictionary<Guid, Guid> connectedRooms = new();          // RoomId -> ConnectionId
    private readonly ConcurrentDictionary<Guid, Guid> roomsByClientConnection = new(); // ConnectionId -> RoomId

    Role IRoomConnectionRegistry.GetRole(Guid roomId)
        => hostedRooms.ContainsKey(roomId) ? Role.Host
            : connectedRooms.ContainsKey(roomId) ? Role.Client
            : throw new RoomNotRegistredException(roomId);

    bool IRoomConnectionRegistry.IsHosting(Guid roomId) => hostedRooms.ContainsKey(roomId);
    bool IRoomConnectionRegistry.IsConnected(Guid roomId) => connectedRooms.ContainsKey(roomId);

    void IRoomConnectionRegistry.RegisterServerConnection(Guid roomId, Guid serverConnectionId)
    {
        hostedRooms[roomId] = serverConnectionId;
        roomsByServerConnection[serverConnectionId] = roomId;
    }

    void IRoomConnectionRegistry.UnregisterServerConnection(Guid roomId)
    {
        if (hostedRooms.TryRemove(roomId, out var serverConnectionId))
        {
            roomsByServerConnection.TryRemove(serverConnectionId ?? Guid.Empty, out _);
        }
    }

    void IRoomConnectionRegistry.DetachServerConnection(Guid roomId)
    {
        if (hostedRooms.TryGetValue(roomId, out var serverConnectionId) && serverConnectionId.HasValue)
        {
            roomsByServerConnection.TryRemove(serverConnectionId.Value, out _);
        }
        hostedRooms[roomId] = null;
    }

    Guid IRoomConnectionRegistry.GetServerConnectionIdByRoomId(Guid roomId)
    {
        hostedRooms.TryGetValue(roomId, out var currentConnectionId);

        if (currentConnectionId.HasValue)
        {
            return currentConnectionId.Value;
        }

        throw new RoomNotRegistredException(roomId);
    }

    Guid IRoomConnectionRegistry.GetRoomIdByServerConnectionId(Guid serverConnectionId)
        => roomsByServerConnection.TryGetValue(serverConnectionId, out var roomId)
            ? roomId : throw new ConnectionNotRegistredException(serverConnectionId);

    bool IRoomConnectionRegistry.TryGetServerConnectionIdByRoomId(Guid? roomId, out Guid connectionId)
    {
        hostedRooms.TryGetValue(roomId ?? Guid.Empty, out var currentConnectionId);

        if (currentConnectionId.HasValue)
        {
            connectionId = currentConnectionId.Value;
            return true;
        }

        connectionId = Guid.Empty;
        return false;
    }

    bool IRoomConnectionRegistry.TryGetRoomIdByServerConnectionId(Guid? serverConnectionId, out Guid roomId)
        => roomsByServerConnection.TryGetValue(serverConnectionId ?? Guid.Empty, out roomId);

    int IRoomConnectionRegistry.GetServerConnectionCount() => hostedRooms.Values.Count(v => v.HasValue);

    void IRoomConnectionRegistry.RegisterClientConnection(Guid roomId, Guid connectionId)
    {
        connectedRooms[roomId] = connectionId;
        roomsByClientConnection[connectionId] = roomId;
    }

    void IRoomConnectionRegistry.UnregisterClientConnection(Guid connectionId)
    {
        if (roomsByClientConnection.TryRemove(connectionId, out var roomId))
        {
            connectedRooms.TryRemove(roomId, out _);
        }
    }

    Guid IRoomConnectionRegistry.GetClientConnectionIdByRoomId(Guid roomId)
        => connectedRooms.TryGetValue(roomId, out var id) ? id : throw new RoomNotRegistredException(roomId);

    Guid IRoomConnectionRegistry.GetRoomIdByClientConnectionId(Guid connectionId)
        => roomsByClientConnection.TryGetValue(connectionId, out var roomId) ? roomId : throw new ConnectionNotRegistredException(connectionId);

    bool IRoomConnectionRegistry.TryGetClientConnectionIdByRoomId(Guid? roomId, out Guid connectionId)
        => connectedRooms.TryGetValue(roomId ?? Guid.Empty, out connectionId);

    bool IRoomConnectionRegistry.TryGetRoomIdByClientConnectionId(Guid? connectionId, out Guid roomId)
        => roomsByClientConnection.TryGetValue(connectionId ?? Guid.Empty, out roomId);

    int IRoomConnectionRegistry.GetClientConnectionCount() => connectedRooms.Count;
}
