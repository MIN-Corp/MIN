using System.Collections.Concurrent;
using System.Timers;
using MIN.Core.Entities;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Services.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces.Persistence;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Services.Persistence;

/// <inheritdoc cref="IRoomPersistenceService"/>
public class RoomPersistenceService : IRoomPersistenceService
{
    private const int AutosaveIntervalMs = 60_000;

    private readonly List<Room> loadedRooms = [];
    private readonly ConcurrentDictionary<Guid, byte> dirtyRoomIds = [];
    private readonly TaskCompletionSource loadingCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly System.Timers.Timer autosaveTimer = new()
    {
        Interval = AutosaveIntervalMs
    };

    private readonly IRoomFileStore fileStore;
    private readonly IRoomSnapshotMapper mapper;
    private readonly IEventBus eventBus;
    private readonly IRoomStore roomStore;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomPersistenceService"/>
    /// </summary>
    public RoomPersistenceService(IRoomFileStore fileStore,
        IRoomSnapshotMapper mapper,
        IEventBus eventBus,
        IRoomStore roomStore,
        ILoggerProvider logger)
    {
        this.fileStore = fileStore;
        this.mapper = mapper;
        this.eventBus = eventBus;
        this.roomStore = roomStore;
        this.logger = logger;

        autosaveTimer.Elapsed += AutosaveTimer_Elapsed;
    }

    Task IRoomPersistenceService.StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var snapshot in fileStore.LoadAll())
            {
                try
                {
                    mapper.RestoreSnapshot(snapshot);
                    loadedRooms.Add(snapshot.Room);
                }
                catch (Exception ex)    // per-room isolation: one bad file never stops the rest
                {
                    logger.Log($"Не удалось восстановить комнату из файла: {ex.Message}", LogLevel.Error);
                }
            }

            eventBus.Subscribe<RoomWentOfflineEvent>(OnRoomWentOffline);
            eventBus.Subscribe<RoomDestroyedEvent>(OnRoomDestroyed);

            autosaveTimer.Start();
        }
        finally
        {
            loadingCompletion.TrySetResult();
        }

        return Task.CompletedTask;
    }

    async Task<IReadOnlyList<Room>> IRoomPersistenceService.GetLoadedRoomsAsync()
    {
        await loadingCompletion.Task;
        lock (loadedRooms)
        {
            return loadedRooms.ToList();
        }
    }

    void IRoomPersistenceService.MarkDirty(Guid roomId) => dirtyRoomIds.TryAdd(roomId, 0);

    private async void AutosaveTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        var dirtyRooms = dirtyRoomIds.Keys.ToList();
        foreach (var roomId in dirtyRooms)
        {
            await SaveRoomAsync(roomId);
        }
    }

    private async Task OnRoomWentOffline(RoomWentOfflineEvent e, CancellationToken ct)
    {
        await SaveRoomAsync(e.RoomId);   // final save regardless of aliveness
    }

    private Task OnRoomDestroyed(RoomDestroyedEvent e, CancellationToken ct)
    {
        var room = loadedRooms.Where(x => x.Id == e.RoomId).FirstOrDefault();
        if (room != null)
        {
            loadedRooms.Remove(room);
        }

        fileStore.Delete(e.RoomId);
        dirtyRoomIds.TryRemove(e.RoomId, out _);

        return Task.CompletedTask;
    }

    private async Task SaveRoomAsync(Guid roomId)
    {
        try
        {
            var snapshot = mapper.BuildSnapshot(roomId);

            if (snapshot == null)   // room vanished from store without a destroy event
            {
                dirtyRoomIds.TryRemove(roomId, out _);
                return;
            }

            await fileStore.SaveAsync(roomId, snapshot);
            dirtyRoomIds.TryRemove(roomId, out _);
        }
        catch (Exception ex)
        {
            logger.Log($"Не удалось сохранить комнату {roomId}: {ex.Message}", LogLevel.Error);
        }
    }

    async Task IRoomPersistenceService.StopAsync(CancellationToken cancellationToken)
    {
        autosaveTimer.Stop();
        autosaveTimer.Dispose();

        foreach (var room in roomStore.GetAllRooms().ToList())
        {
            try
            {
                var snapshot = mapper.BuildSnapshot(room.Id);
                if (snapshot != null)
                {
                    await fileStore.SaveAsync(room.Id, snapshot);
                    dirtyRoomIds.TryRemove(room.Id, out _);
                }
            }
            catch (Exception ex)
            {
                logger.Log($"Не удалось сохранить комнату {room.Id} при выходе: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
