using FluentAssertions;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Events;
using MIN.Core.Events.Services;
using MIN.Core.Services.Contracts.Interfaces.Persistence;
using MIN.Core.Services.Persistence;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Models.Persistence;
using MIN.Core.Stores.Services;
using MIN.Tests.Common;
using Moq;
using MIN.Core.Events.Contracts.Interfaces;
using Xunit;

namespace MIN.Core.Services.Tests;

/// <summary>
/// Тесты на <see cref="RoomPersistenceService"/>
/// </summary>
public class RoomPersistenceServiceTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly TestLoggerProvider logger = new();
    private readonly IEventBus eventBus;
    private readonly Mock<IRoomFileStore> fileStore = new();
    private readonly Mock<IRoomSnapshotMapper> mapper = new();
    private readonly Mock<IMessageMigrationService> migration = new();
    private readonly IRoomStore roomStore;
    private readonly IRoomPersistenceService service;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomPersistenceServiceTests"/>
    /// </summary>
    public RoomPersistenceServiceTests()
    {
        eventBus = new InMemoryEventBus(logger);
        roomStore = new RoomStore(rooms.Factory);
        migration.Setup(m => m.Apply(It.IsAny<List<MIN.Core.Messaging.Contracts.Interfaces.IMessage>>(), It.IsAny<Room>())).Returns(0);
        fileStore.Setup(f => f.LoadAll()).Returns([]);
        service = new RoomPersistenceService(fileStore.Object, mapper.Object, eventBus, roomStore, migration.Object, logger);
    }

    private static Room CreateRoom(Guid? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Комната",
            HostParticipant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Хост" },
        };

    /// <summary>
    /// Старт загружает комнаты из снапшотов
    /// </summary>
    [Fact]
    public async Task StartShouldLoadSnapshots()
    {
        // Arrange
        var room = CreateRoom();
        var snapshot = new RoomSnapshot { Room = room, Messages = [] };
        fileStore.Setup(f => f.LoadAll()).Returns([snapshot]);

        // Act
        await service.StartAsync();
        var loaded = await service.GetLoadedRoomsAsync();

        // Assert
        loaded.Should().ContainSingle().Which.Id.Should().Be(room.Id);
        mapper.Verify(m => m.RestoreSnapshot(snapshot), Times.Once);
    }

    /// <summary>
    /// Битый снапшот пропускается с ошибкой в логе
    /// </summary>
    [Fact]
    public async Task BrokenSnapshotShouldBeSkipped()
    {
        // Arrange
        var room = CreateRoom();
        var snapshot = new RoomSnapshot { Room = room, Messages = [] };
        fileStore.Setup(f => f.LoadAll()).Returns([snapshot]);
        mapper.Setup(m => m.RestoreSnapshot(It.IsAny<RoomSnapshot>())).Throws(new InvalidOperationException("битый"));

        // Act
        await service.StartAsync();
        var loaded = await service.GetLoadedRoomsAsync();

        // Assert
        loaded.Should().BeEmpty();
        logger.HasErrors.Should().BeTrue();
    }

    /// <summary>
    /// Уход комнаты в оффлайн сохраняет её снапшот
    /// </summary>
    [Fact]
    public async Task RoomWentOfflineShouldSave()
    {
        // Arrange
        var room = CreateRoom();
        var snapshot = new RoomSnapshot { Room = room, Messages = [] };
        mapper.Setup(m => m.BuildSnapshot(room.Id)).Returns(snapshot);
        await service.StartAsync();

        // Act
        await eventBus.PublishAsync(new RoomWentOfflineEvent { RoomId = room.Id }, CancellationToken.None);
        await TestWait.UntilAsync(() => fileStore.Invocations.Any(i => i.Method.Name == "SaveAsync"));

        // Assert
        fileStore.Verify(f => f.SaveAsync(room.Id, snapshot), Times.Once);
    }

    /// <summary>
    /// Уничтожение комнаты удаляет файл и убирает её из загруженных
    /// </summary>
    [Fact]
    public async Task RoomDestroyedShouldDeleteFile()
    {
        // Arrange
        var room = CreateRoom();
        var snapshot = new RoomSnapshot { Room = room, Messages = [] };
        fileStore.Setup(f => f.LoadAll()).Returns([snapshot]);
        await service.StartAsync();

        // Act
        await eventBus.PublishAsync(new RoomDestroyedEvent { RoomId = room.Id }, CancellationToken.None);
        await TestWait.UntilAsync(() => fileStore.Invocations.Any(i => i.Method.Name == "Delete"));
        var loaded = await service.GetLoadedRoomsAsync();

        // Assert
        fileStore.Verify(f => f.Delete(room.Id), Times.Once);
        loaded.Should().BeEmpty();
    }

    /// <summary>
    /// Остановка сохраняет все комнаты из хранилища
    /// </summary>
    [Fact]
    public async Task StopShouldSaveAllRooms()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var snapshot = new RoomSnapshot { Room = room, Messages = [] };
        mapper.Setup(m => m.BuildSnapshot(room.Id)).Returns(snapshot);
        await service.StartAsync();

        // Act
        await service.StopAsync();

        // Assert
        fileStore.Verify(f => f.SaveAsync(room.Id, snapshot), Times.Once);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        service.StopAsync().GetAwaiter().GetResult();
        rooms.Dispose();
    }
}
