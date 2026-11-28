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
    /// Обновление бана меняет причину и дату, а IP и флаг ведут себя по чекбоксу
    /// </summary>
    [Fact]
    public void UpdateBanShouldReplaceReasonAndDateAndToggleIpFlag()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();
        service.Ban(room.Id, participantId, "Вася", "спам", DateTimeOffset.Now.AddDays(1), "10.0.0.1", isIpBanned: true);
        var newUntil = DateTimeOffset.Now.AddMonths(2);

        // Act
        var updated = service.UpdateBan(room.Id, participantId, "спам и ещё спам", newUntil, alsoBanIp: false);

        // Assert — адрес не сбрасывается, флаг выключается
        updated.Should().NotBeNull();
        updated!.IpAddress.Should().Be("10.0.0.1");
        updated.IsIpBanned.Should().BeFalse();
        updated.Reason.Should().Be("спам и ещё спам");
        updated.BannedUntil.Should().Be(newUntil);
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
        var updated = service.UpdateBan(room.Id, Guid.NewGuid(), "Вы забанены", DateTimeOffset.Now, false);

        // Assert
        updated.Should().BeNull();
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

    /// <summary>
    /// Бан по IP сохраняет адрес и флаг
    /// </summary>
    [Fact]
    public void BanWithIpShouldStoreAddressAndFlag()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();

        // Act
        var ban = service.Ban(room.Id, participantId, "Вася", "спам", DateTimeOffset.Now.AddDays(1), "10.0.0.1", isIpBanned: true);

        // Assert
        ban.IpAddress.Should().Be("10.0.0.1");
        ban.IsIpBanned.Should().BeTrue();
        room.LocalRoomSettings.BannedParticipants[participantId].Should().BeSameAs(ban);
    }

    /// <summary>
    /// Бан по IP находится по адресу
    /// </summary>
    [Fact]
    public void TryGetActiveBanByIpShouldReturnBanForBannedIp()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var expected = service.Ban(room.Id, Guid.NewGuid(), "Вася", "спам", DateTimeOffset.Now.AddDays(1), "10.0.0.1", isIpBanned: true);

        // Act
        var result = service.TryGetActiveBanByIp(room.Id, "10.0.0.1", out var ban);

        // Assert
        result.Should().BeTrue();
        ban.Should().BeSameAs(expected);
    }

    /// <summary>
    /// Чужой IP-адрес не попадает под бан
    /// </summary>
    [Fact]
    public void TryGetActiveBanByIpShouldReturnFalseForOtherIp()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        service.Ban(room.Id, Guid.NewGuid(), "Вася", "спам", DateTimeOffset.Now.AddDays(1), "10.0.0.1", isIpBanned: true);

        // Act
        var result = service.TryGetActiveBanByIp(room.Id, "10.0.0.2", out var ban);

        // Assert
        result.Should().BeFalse();
        ban.Should().BeNull();
    }

    /// <summary>
    /// Известный адрес с выключенным флагом не блокирует по IP
    /// </summary>
    [Fact]
    public void TryGetActiveBanByIpShouldReturnFalseWhenIpBanDisabled()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        service.Ban(room.Id, Guid.NewGuid(), "Вася", "спам", DateTimeOffset.Now.AddDays(1), "10.0.0.1", isIpBanned: false);

        // Act
        var result = service.TryGetActiveBanByIp(room.Id, "10.0.0.1", out var ban);

        // Assert
        result.Should().BeFalse();
        ban.Should().BeNull();
    }

    /// <summary>
    /// Истёкший бан по IP удаляется лениво при проверке
    /// </summary>
    [Fact]
    public void TryGetActiveBanByIpShouldRemoveExpiredBanAndMarkDirty()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();
        service.Ban(room.Id, participantId, "Вася", "спам", DateTimeOffset.Now.AddSeconds(-1), "10.0.0.1", isIpBanned: true);

        // Act
        var result = service.TryGetActiveBanByIp(room.Id, "10.0.0.1", out _);

        // Assert
        result.Should().BeFalse();
        room.LocalRoomSettings.BannedParticipants.Should().BeEmpty();
        persistence.Verify(p => p.MarkDirty(room.Id), Times.Exactly(2));
    }

    /// <summary>
    /// Выключенный флаг можно включить снова — адрес сохраняется
    /// </summary>
    [Fact]
    public void UpdateBanShouldAllowReenablingIpBan()
    {
        // Arrange
        var room = CreateRoom();
        roomStore.Register(room);
        var participantId = Guid.NewGuid();
        service.Ban(room.Id, participantId, "Вася", "спам", DateTimeOffset.Now.AddDays(1), "10.0.0.1", isIpBanned: false);

        // Act
        var reenabled = service.UpdateBan(room.Id, participantId, "спам", DateTimeOffset.Now.AddMonths(2), alsoBanIp: true);

        // Assert
        reenabled.Should().NotBeNull();
        reenabled!.IsIpBanned.Should().BeTrue();
        reenabled.IpAddress.Should().Be("10.0.0.1");
        persistence.Verify(p => p.MarkDirty(room.Id), Times.Exactly(2));
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => rooms.Dispose();
}
