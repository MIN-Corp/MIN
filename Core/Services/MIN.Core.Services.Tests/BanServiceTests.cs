using FluentAssertions;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Core.Services.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Services;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Core.Services.Tests;

/// <summary>
/// Тесты на <see cref="MIN.Core.Services.Moderation.BanService"/>
/// </summary>
public class BanServiceTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly Mock<IRoomPersistenceService> persistence = new();
    private readonly IRoomStore roomStore;
    private readonly IBanService service;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BanServiceTests"/>
    /// </summary>
    public BanServiceTests()
    {
        roomStore = new RoomStore(rooms.Factory);
        service = new Moderation.BanService(roomStore, persistence.Object);
    }

    /// <summary>
    /// Создать комнату с хостом
    /// </summary>
    private Room CreateRoom()
        => new()
        {
            Name = "Комната",
            HostParticipant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Хост" },
            MaximumParticipants = 10,
        };

    /// <summary>
    /// Бан сохраняет информацию о бане и помечает комнату dirty
    /// </summary>
    [Fact]
    public void BanShouldStoreBanInfoAndMarkDirty()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();
        var until = DateTimeOffset.Now.AddDays(1);

        // Act
        var ban = service.Ban(room.Id, participantId, "Вася", "спам", until);

        // Assert
        ban.ParticipantId.Should().Be(participantId);
        ban.Name.Should().Be("Вася");
        ban.BannedUntil.Should().Be(until);
        room.LocalRoomSettings.BannedParticipants[participantId].Should().BeSameAs(ban);
        persistence.Verify(p => p.MarkDirty(room.Id), Times.Once);
    }

    /// <summary>
    /// Бан без даты считается вечным и формирует соответствующее сообщение
    /// </summary>
    [Fact]
    public void BanWithNullDateShouldMeanPermanent()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();

        // Act
        var ban = service.Ban(room.Id, participantId, "Вася", "", null);

        // Assert
        ban.BannedUntil.Should().BeNull();
        ban.GetBanMessage().Should().Be("Вы забанены навсегда");
    }

    /// <summary>
    /// Бан в незарегистрированной комнате выбрасывает исключение
    /// </summary>
    [Fact]
    public void BanShouldThrowForUnknownRoom()
        => service.Invoking(s => s.Ban(Guid.NewGuid(), Guid.NewGuid(), "Вася", "", null))
            .Should().Throw<InvalidOperationException>().WithMessage("*не нашлось*");

    /// <summary>
    /// Разбан удаляет запись и помечает комнату dirty
    /// </summary>
    [Fact]
    public void UnbanShouldRemoveBanAndMarkDirty()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();
        service.Ban(room.Id, participantId, "Вася", "", null);

        // Act
        var removed = service.Unban(room.Id, participantId);

        // Assert
        removed.Should().BeTrue();
        room.LocalRoomSettings.BannedParticipants.Should().BeEmpty();
        persistence.Verify(p => p.MarkDirty(room.Id), Times.Exactly(2));
    }

    /// <summary>
    /// Разбан несуществующего участника возвращает false
    /// </summary>
    [Fact]
    public void UnbanShouldReturnFalseForUnknownParticipant()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);

        // Act
        var removed = service.Unban(room.Id, Guid.NewGuid());

        // Assert
        removed.Should().BeFalse();
    }

    /// <summary>
    /// Обновление бана меняет только дату разбана
    /// </summary>
    [Fact]
    public void UpdateBanShouldReplaceDateOnly()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();
        var original = service.Ban(room.Id, participantId, "Вася", "спам", DateTimeOffset.Now.AddDays(1));
        var newUntil = DateTimeOffset.Now.AddMonths(2);

        // Act
        var updated = service.UpdateBan(room.Id, participantId, newUntil);

        // Assert
        updated.Should().BeTrue();
        var stored = room.LocalRoomSettings.BannedParticipants[participantId];
        stored.BannedUntil.Should().Be(newUntil);
        stored.Name.Should().Be(original.Name);
        stored.Reason.Should().Be(original.Reason);
        persistence.Verify(p => p.MarkDirty(room.Id), Times.Exactly(2));
    }

    /// <summary>
    /// Обновление бана несуществующего участника возвращает false
    /// </summary>
    [Fact]
    public void UpdateBanShouldReturnFalseForUnknownParticipant()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);

        // Act
        var updated = service.UpdateBan(room.Id, Guid.NewGuid(), DateTimeOffset.Now);

        // Assert
        updated.Should().BeFalse();
    }

    /// <summary>
    /// Активный бан находится по участнику
    /// </summary>
    [Fact]
    public void TryGetActiveBanShouldReturnBanForBannedParticipant()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();
        var expected = service.Ban(room.Id, participantId, "Вася", "спам", DateTimeOffset.Now.AddDays(1));

        // Act
        var result = service.TryGetActiveBan(room.Id, participantId, out var ban);

        // Assert
        result.Should().BeTrue();
        ban.Should().BeSameAs(expected);
    }

    /// <summary>
    /// Истёкший бан удаляется лениво и помечает комнату dirty
    /// </summary>
    [Fact]
    public void TryGetActiveBanShouldRemoveExpiredBanAndMarkDirty()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();
        service.Ban(room.Id, participantId, "Вася", "спам", DateTimeOffset.Now.AddSeconds(-1));

        // Act
        var result = service.TryGetActiveBan(room.Id, participantId, out _);

        // Assert
        result.Should().BeFalse();
        room.LocalRoomSettings.BannedParticipants.Should().BeEmpty();
        persistence.Verify(p => p.MarkDirty(room.Id), Times.Exactly(2));
    }

    /// <summary>
    /// Для не забаненного участника активный бан не находится
    /// </summary>
    [Fact]
    public void TryGetActiveBanShouldReturnFalseForUnknownParticipant()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);

        // Act
        var result = service.TryGetActiveBan(room.Id, Guid.NewGuid(), out _);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Список банов отсортирован по имени и не содержит истёкших
    /// </summary>
    [Fact]
    public void GetBannedParticipantsShouldReturnSortedActiveBansWithoutExpired()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        service.Ban(room.Id, Guid.NewGuid(), "Бета", "", null);
        service.Ban(room.Id, Guid.NewGuid(), "Альфа", "", null);
        service.Ban(room.Id, Guid.NewGuid(), "Гамма", "", DateTimeOffset.Now.AddSeconds(-1));

        // Act
        var bans = service.GetBannedParticipants(room.Id);

        // Assert
        bans.Select(b => b.Name).Should().ContainInOrder("Альфа", "Бета").And.NotContain("Гамма");
        persistence.Verify(p => p.MarkDirty(room.Id), Times.Exactly(4));
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => rooms.Dispose();
}
