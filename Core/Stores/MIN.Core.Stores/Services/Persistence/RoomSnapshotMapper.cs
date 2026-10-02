using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Models.Persistence;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Stores.Services.Persistence;

/// <inheritdoc cref="IRoomSnapshotMapper"/>
public sealed class RoomSnapshotMapper : IRoomSnapshotMapper
{
    private readonly IRoomStore roomStore;
    private readonly IRoomFactory roomFactory;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomSnapshotMapper"/>
    /// </summary>
    public RoomSnapshotMapper(IRoomStore roomStore,
        IRoomFactory roomFactory,
        ILoggerProvider logger)
    {
        this.roomStore = roomStore;
        this.roomFactory = roomFactory;
        this.logger = logger;
    }

    RoomSnapshot? IRoomSnapshotMapper.BuildSnapshot(Guid roomId)
    {
        if (!roomStore.TryGetRoom(roomId, out var room))
        {
            return null;
        }

        var context = roomFactory.GetOrCreateContext(roomId);

        return new RoomSnapshot
        {
            Room = room.Clone(),
            Messages = context.Messages.GetHistory().ToList(),
            SubRooms = context.SubRooms.CreateSnapshot(),
        };
    }

    void IRoomSnapshotMapper.RestoreSnapshot(RoomSnapshot snapshot)
    {
        var room = snapshot.Room;
        room.IsOnline = false;

        roomStore.Register(room);

        var context = roomFactory.GetOrCreateContext(room.Id);

        foreach (var message in snapshot.Messages)
        {
            context.Messages.AddMessage(message);
        }

        if (snapshot.SubRooms != null)
        {
            context.SubRooms.RestoreState(snapshot.SubRooms);
        }

        context.Participants.MarkAllParticipansOffline();
        room.TotalMessageCount = context.Messages.GetMessageCount();

        logger.Log($"Комната {room.Id} '{room.Name}' восстановлена ({room.TotalMessageCount} сообщений)");
    }
}
