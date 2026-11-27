using System.Text;
using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Core.Cryptography;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Models.Persistence;
using MIN.Core.Stores.Services;
using MIN.Core.Stores.Services.Persistence;
using MIN.Tests.Common;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="RoomFileStore"/>
/// </summary>
public class RoomFileStoreTests : IDisposable
{
    private readonly TestIdentityDataPathProvider paths = new();
    private readonly TestRoomFactory roomFactory = new();
    private readonly TestLoggerProvider logger = new();
    private readonly IRoomStore roomStore;
    private readonly IRoomFileStore fileStore;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomFileStoreTests"/>
    /// </summary>
    public RoomFileStoreTests()
    {
        roomStore = new RoomStore(roomFactory.Factory);
        var encryptor = new RoomFileEncryptor(logger, new EphemeralDataProtectionProvider(), paths);
        fileStore = new RoomFileStore(encryptor, TestSerializer.Create(), paths, roomStore, logger);
    }

    /// <summary>
    /// Создать комнату с хостом
    /// </summary>
    private static Room CreateRoom(string name = "Комната")
        => new()
        {
            Name = name,
            HostParticipant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Хост" },
            MaximumParticipants = 10,
        };

    /// <summary>
    /// Сохранение и загрузка возвращают комнату с сообщениями
    /// </summary>
    [Fact]
    public async Task SaveLoadShouldRoundTrip()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var snapshot = new RoomSnapshot
        {
            Room = room,
            Messages =
            [
                new ChatTextMessage { Content = "публичное", SenderName = "Автор" },
                new ChatTextMessage { Content = "личное", SenderName = "Автор", RecipientId = Guid.NewGuid() },
            ],
            SubRooms = new SubRoomsSnapshot { SubRooms = [], NextId = 5 },
        };

        // Act
        await fileStore.SaveAsync(room.Id, snapshot);
        var exists = fileStore.Exists(room.Id);
        var loaded = fileStore.LoadAll();

        // Assert
        exists.Should().BeTrue();
        loaded.Should().ContainSingle();
        loaded[0].Room.Id.Should().Be(room.Id);
        loaded[0].Room.Name.Should().Be("Комната");
        loaded[0].Messages.Should().HaveCount(2);
        loaded[0].Messages.Should().AllBeOfType<ChatTextMessage>();
        loaded[0].SubRooms!.NextId.Should().Be(5);
    }

    /// <summary>
    /// Файл комнаты начинается с сигнатуры MINR и версии формата
    /// </summary>
    [Fact]
    public async Task SavedFileShouldHaveMagicHeader()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);

        // Act
        await fileStore.SaveAsync(room.Id, new RoomSnapshot { Room = room, Messages = [] });
        var file = Directory.GetFiles(paths.RoomsDirectory, "*.mr").Single();
        var bytes = await File.ReadAllBytesAsync(file);

        // Assert
        Encoding.ASCII.GetString(bytes[..4]).Should().Be("MINR");
        bytes[4].Should().Be(1);
    }

    /// <summary>
    /// Повреждённый файл пропускается и откладывается в .bak
    /// </summary>
    [Fact]
    public async Task CorruptedFileShouldBeSkippedAndBackedUp()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        await fileStore.SaveAsync(room.Id, new RoomSnapshot { Room = room, Messages = [] });
        var file = Directory.GetFiles(paths.RoomsDirectory, "*.mr").Single();
        await File.WriteAllBytesAsync(file, [1, 2, 3, 4, 5]);

        // Act
        var loaded = fileStore.LoadAll();

        // Assert
        loaded.Should().BeEmpty();
        File.Exists(file + ".bak").Should().BeTrue();
        logger.HasErrors.Should().BeTrue();
    }

    /// <summary>
    /// Файл с неверной сигнатурой пропускается
    /// </summary>
    [Fact]
    public async Task WrongMagicShouldBeSkipped()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        await fileStore.SaveAsync(room.Id, new RoomSnapshot { Room = room, Messages = [] });
        var file = Directory.GetFiles(paths.RoomsDirectory, "*.mr").Single();
        var bytes = await File.ReadAllBytesAsync(file);
        bytes[0] = (byte)'X';
        await File.WriteAllBytesAsync(file, bytes);

        // Act
        var loaded = fileStore.LoadAll();

        // Assert
        loaded.Should().BeEmpty();
    }

    /// <summary>
    /// Delete удаляет файл комнаты
    /// </summary>
    [Fact]
    public async Task DeleteShouldRemoveFile()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        await fileStore.SaveAsync(room.Id, new RoomSnapshot { Room = room, Messages = [] });

        // Act
        fileStore.Delete(room.Id);

        // Assert
        fileStore.Exists(room.Id).Should().BeFalse();
        Directory.GetFiles(paths.RoomsDirectory, "*.mr").Should().BeEmpty();
    }

    /// <summary>
    /// Дубли файлов одной комнаты схлопываются при загрузке
    /// </summary>
    [Fact]
    public async Task DuplicateFilesShouldCollapseToOne()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        await fileStore.SaveAsync(room.Id, new RoomSnapshot { Room = room, Messages = [] });
        var file = Directory.GetFiles(paths.RoomsDirectory, "*.mr").Single();
        File.Copy(file, Path.Combine(paths.RoomsDirectory, $"{room.Id}.mr"));

        // Act
        var loaded = fileStore.LoadAll();

        // Assert
        loaded.Should().ContainSingle();
        Directory.GetFiles(paths.RoomsDirectory, "*.mr").Should().HaveCount(1);
    }

    /// <summary>
    /// Недопустимые символы в имени комнаты заменяются в имени файла
    /// </summary>
    [Fact]
    public async Task InvalidRoomNameCharsShouldBeSanitized()
    {
        // Arrange
        var room = CreateRoom("""a<b>c:d"/\|?*""");
        roomStore.Register(room);

        // Act
        await fileStore.SaveAsync(room.Id, new RoomSnapshot { Room = room, Messages = [] });
        var file = Directory.GetFiles(paths.RoomsDirectory, "*.mr").Single();
        var loaded = fileStore.LoadAll();

        // Assert
        Path.GetFileName(file).Should().NotContainAny("<", ">", ":", "\"", "/", "\\", "|", "?", "*");
        loaded.Should().ContainSingle();
        loaded[0].Room.Name.Should().Be("""a<b>c:d"/\|?*""");
    }

    /// <summary>
    /// Список банов комнаты переживает сохранение и загрузку
    /// </summary>
    [Fact]
    public async Task SaveLoadShouldRoundTripBannedParticipants()
    {
        // Arrange
        var room = CreateRoom();
        var bannedId = Guid.NewGuid();
        room.LocalRoomSettings.BannedParticipants[bannedId] =
            new BanInfo(bannedId, "Вася", "спам", DateTimeOffset.Now.AddDays(1));
        room.LocalRoomSettings.BannedParticipants[Guid.NewGuid()] =
            new BanInfo(Guid.NewGuid(), "Петя", "", null);
        roomStore.Register(room);

        // Act
        await fileStore.SaveAsync(room.Id, new RoomSnapshot
        {
            Room = room,
            Messages = [],
            SubRooms = new SubRoomsSnapshot { SubRooms = [], NextId = 1 },
        });
        var loaded = fileStore.LoadAll();

        // Assert
        var settings = loaded.Single().Room.LocalRoomSettings;
        settings.BannedParticipants.Should().HaveCount(2);
        settings.BannedParticipants[bannedId].Name.Should().Be("Вася");
        settings.BannedParticipants.Should().Contain(b => b.Value.BannedUntil == null);
    }

    /// <summary>
    /// LoadAll из пустой директории возвращает пустой список
    /// </summary>
    [Fact]
    public void LoadAllEmptyDirectoryShouldReturnEmpty()
    {
        // Act
        var loaded = fileStore.LoadAll();

        // Assert
        loaded.Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        roomFactory.Dispose();
        paths.Dispose();
    }
}
