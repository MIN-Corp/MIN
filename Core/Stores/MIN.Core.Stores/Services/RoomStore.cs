using System.Collections.Concurrent;
using MIN.Core.Entities;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models;

namespace MIN.Core.Stores.Services;

/// <inheritdoc cref="IRoomStore"/>
public sealed class RoomStore : IRoomStore
{
    private readonly IRoomFactory roomFactory;
    private readonly ConcurrentDictionary<Guid, Room> roomsById = new();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomStore"/>
    /// </summary>
    public RoomStore(IRoomFactory roomFactory)
    {
        this.roomFactory = roomFactory;
    }

    bool IRoomStore.RoomExists(Guid roomId)
        => roomsById.ContainsKey(roomId);

    Room IRoomStore.GetRoom(Guid roomId)
    {
        if (roomsById.TryGetValue(roomId, out var room))
        {
            return room;
        }

        throw new InvalidOperationException($"Комнаты с {roomId} не нашлось");
    }

    bool IRoomStore.TryGetRoom(Guid roomId, out Room room)
    {
        if (roomsById.TryGetValue(roomId, out room!))
        {
            return true;
        }

        return false;
    }

    Room IRoomStore.GetRoomFor(Guid participantId, Guid roomId, bool asRejoin)
    {
        if (roomsById.TryGetValue(roomId, out var room))
        {
            var context = roomFactory.GetOrCreateContext(roomId);
            var snapshot = room.Clone();
            if (!asRejoin)
            {
                snapshot.ChatHistory = context.Messages.GetRecentHistory()
                   .Where(x => x.IsPublic || x.RecipientId == participantId || x.SenderId == participantId)
                   .ToList();
            }
            snapshot.TotalMessageCount = GetMessagesCountFor(context, participantId);
            snapshot.LocalRoomSettings.NotificationsEnabled = false;
            return snapshot;
        }

        throw new InvalidOperationException($"Комнаты с {roomId} не нашлось");
    }

    int IRoomStore.GetRoomChatHistoryCountFor(Guid participantId, Guid roomId)
    {
        if (roomsById.TryGetValue(roomId, out _))
        {
            var context = roomFactory.GetOrCreateContext(roomId);
            return GetMessagesCountFor(context, participantId);
        }

        throw new InvalidOperationException($"Комнаты с {roomId} не нашлось");
    }

    private static int GetMessagesCountFor(RoomContext context, Guid participantId)
        => context.Messages.GetHistory()
        .Where(x => x.IsPublic || x.RecipientId == participantId || x.SenderId == participantId).Count();

    Guid IRoomStore.GetRoomHostParticipantId(Guid roomId)
        => roomsById.TryGetValue(roomId, out var room) ? room.HostParticipant.Id : throw new KeyNotFoundException();

    void IRoomStore.Register(Room room)
    {
        roomsById[room.Id] = room;
        roomFactory.GetOrCreateContext(room.Id).Participants.Bind(room.CurrentParticipants);
    }

    void IRoomStore.Remove(Guid roomId)
    {
        roomsById.TryRemove(roomId, out _);
    }

    IEnumerable<Room> IRoomStore.GetAllRooms()
        => roomsById.Values;
}
