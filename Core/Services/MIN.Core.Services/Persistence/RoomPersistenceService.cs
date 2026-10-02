using System.Timers;
using MIN.Common.Core.Contracts.Interfaces;
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
public class RoomPersistenceService : IRoomPersistenceService, IHostedService
{
    private const int AutosaveIntervalMs = 60_000;

    private readonly List<Room> loadedRooms = [];
    private readonly HashSet<Guid> dirtyRoomIds = [];
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
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
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
        return Task.CompletedTask;
    }

    IReadOnlyList<Room> IRoomPersistenceService.GetLoadedRooms()
    {
        lock (loadedRooms)
        {
            return loadedRooms.ToList();
        }
    }

    void IRoomPersistenceService.MarkDirty(Guid roomId) => dirtyRoomIds.Add(roomId);

    private async void AutosaveTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        foreach (var roomId in dirtyRoomIds)
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
        fileStore.Delete(e.RoomId);
        dirtyRoomIds.Remove(e.RoomId);

        return Task.CompletedTask;
    }

    private async Task SaveRoomAsync(Guid roomId)
    {
        try
        {
            var snapshot = mapper.BuildSnapshot(roomId);

            if (snapshot == null)   // room vanished from store without a destroy event
            {
                dirtyRoomIds.Remove(roomId);
                return;
            }

            await fileStore.SaveAsync(roomId, snapshot);
            dirtyRoomIds.Remove(roomId);
        }
        catch (Exception ex)
        {
            logger.Log($"Не удалось сохранить комнату {roomId}: {ex.Message}", LogLevel.Error);
            // dirty flag stays → retried on next tick
        }
    }

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
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
                    dirtyRoomIds.Remove(room.Id);
                }
            }
            catch (Exception ex)
            {
                logger.Log($"Не удалось сохранить комнату {room.Id} при выходе: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
