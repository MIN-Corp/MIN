using FluentAssertions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Services;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="SubRoomManager"/>
/// </summary>
public class SubRoomManagerTests
{
    private readonly ISubRoomManager manager = new SubRoomManager();
    private readonly ParticipantInfo creator = new() { Id = Guid.NewGuid(), Name = "Создатель" };

    /// <summary>
    /// HostSubRoom создаёт активную подкомнату с растущим id
    /// </summary>
    [Fact]
    public void HostSubRoomShouldCreateActiveWithIncrementingId()
    {
        // Act
        var first = manager.HostSubRoom(creator, SubRoomPurpose.Activity, maximum: 4, includeCreator: true);
        var second = manager.HostSubRoom(creator, SubRoomPurpose.Voice, maximum: null, includeCreator: false);

        // Assert
        first.Id.Should().Be(1);
        second.Id.Should().Be(2);
        first.IsActive.Should().BeTrue();
        first.CreatorId.Should().Be(creator.Id);
        first.Participants.Should().ContainSingle().Which.Id.Should().Be(creator.Id);
        second.Participants.Should().BeEmpty();
    }

    /// <summary>
    /// TryJoinSubRoom добавляет участника один раз
    /// </summary>
    [Fact]
    public void TryJoinSubRoomShouldAddOnce()
    {
        // Arrange
        var subRoom = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 4, includeCreator: false);
        var participant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Участник" };

        // Act
        var first = manager.TryJoinSubRoom(subRoom.Id, participant);
        var second = manager.TryJoinSubRoom(subRoom.Id, participant);

        // Assert
        first.Should().Be(SubRoomJoinOutcome.Success);
        second.Should().Be(SubRoomJoinOutcome.AlreadyJoined);
        manager.GetParticipantCount(subRoom.Id).Should().Be(1);
        manager.IsInSubRoom(subRoom.Id, participant.Id).Should().BeTrue();
    }

    /// <summary>
    /// TryJoinSubRoom уважает лимит участников
    /// </summary>
    [Fact]
    public void TryJoinSubRoomShouldRespectMaximum()
    {
        // Arrange
        var subRoom = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 1, includeCreator: true);
        var extra = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Лишний" };

        // Act
        var outcome = manager.TryJoinSubRoom(subRoom.Id, extra);

        // Assert
        outcome.Should().Be(SubRoomJoinOutcome.MaximumParticipants);
        manager.GetParticipantCount(subRoom.Id).Should().Be(1);
    }

    /// <summary>
    /// TryJoinSubRoom в неизвестную подкомнату возвращает SubRoomNotFound
    /// </summary>
    [Fact]
    public void TryJoinUnknownSubRoomShouldReturnNotFound()
    {
        // Act
        var outcome = manager.TryJoinSubRoom(999, creator);

        // Assert
        outcome.Should().Be(SubRoomJoinOutcome.SubRoomNotFound);
    }

    /// <summary>
    /// Подкомната без лимита принимает сколько угодно участников
    /// </summary>
    [Fact]
    public void TryJoinWithoutMaximumShouldAcceptMany()
    {
        // Arrange
        var subRoom = manager.HostSubRoom(creator, SubRoomPurpose.Voice, includeCreator: false);

        // Act
        for (var i = 0; i < 50; i++)
        {
            manager.TryJoinSubRoom(subRoom.Id, new ParticipantInfo { Id = Guid.NewGuid(), Name = $"Участник {i}" });
        }

        // Assert
        manager.GetParticipantCount(subRoom.Id).Should().Be(50);
    }

    /// <summary>
    /// ActivateSubRoom активирует остановленную подкомнату
    /// </summary>
    [Fact]
    public void ActivateSubRoomShouldActivateStopped()
    {
        // Arrange
        var subRoom = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 4, includeCreator: true);
        manager.TryStopSubRoom(subRoom.Id, creator.Id);

        // Act
        var activated = manager.ActivateSubRoom(subRoom.Id, creator);
        var again = manager.ActivateSubRoom(subRoom.Id, creator);

        // Assert
        activated.Should().BeTrue();
        again.Should().BeFalse();
        manager.GetSubRoom(subRoom.Id)!.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// LeaveSubRoom деактивирует подкомнату, когда вышел последний
    /// </summary>
    [Fact]
    public void LeaveLastParticipantShouldDeactivate()
    {
        // Arrange
        var subRoom = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 4, includeCreator: true);
        var second = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Второй" };
        manager.TryJoinSubRoom(subRoom.Id, second);

        // Act
        var stillActive = manager.LeaveSubRoom(subRoom.Id, second.Id);
        var nowInactive = manager.LeaveSubRoom(subRoom.Id, creator.Id);

        // Assert
        stillActive.Should().BeTrue();
        nowInactive.Should().BeFalse();
        manager.GetSubRoom(subRoom.Id)!.IsActive.Should().BeFalse();
        manager.GetParticipantCount(subRoom.Id).Should().Be(0);
    }

    /// <summary>
    /// TryStopSubRoom доступен только создателю
    /// </summary>
    [Fact]
    public void TryStopSubRoomShouldRequireCreator()
    {
        // Arrange
        var subRoom = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 4, includeCreator: true);
        var stranger = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Чужой" };
        manager.TryJoinSubRoom(subRoom.Id, stranger);

        // Act
        var byStranger = manager.TryStopSubRoom(subRoom.Id, stranger.Id);
        var byCreator = manager.TryStopSubRoom(subRoom.Id, creator.Id);

        // Assert
        byStranger.Should().BeFalse();
        byCreator.Should().BeTrue();
        manager.GetSubRoom(subRoom.Id)!.IsActive.Should().BeFalse();
        manager.GetParticipantCount(subRoom.Id).Should().Be(0);
    }

    /// <summary>
    /// GetSubRoom возвращает копию списка участников
    /// </summary>
    [Fact]
    public void GetSubRoomShouldReturnParticipantsCopy()
    {
        // Arrange
        var subRoom = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 4, includeCreator: true);

        // Act
        var snapshot = manager.GetSubRoom(subRoom.Id)!;
        snapshot.Participants.Clear();

        // Assert
        manager.GetParticipantCount(subRoom.Id).Should().Be(1);
        manager.GetSubRoom(999).Should().BeNull();
    }

    /// <summary>
    /// GetParticipantIds и GetRoomSubRooms возвращают снапшоты
    /// </summary>
    [Fact]
    public void GetParticipantIdsAndListShouldWork()
    {
        // Arrange
        var subRoom = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 4, includeCreator: true);

        // Act
        var ids = manager.GetParticipantIds(subRoom.Id);
        var all = manager.GetRoomSubRooms();

        // Assert
        ids.Should().Equal(creator.Id);
        all.Should().ContainSingle();
        manager.GetParticipantIds(999).Should().BeEmpty();
        manager.GetParticipantCount(999).Should().Be(0);
    }

    /// <summary>
    /// RemoveSubRoom и ClearRoomSubRooms удаляют подкомнаты
    /// </summary>
    [Fact]
    public void RemoveAndClearShouldWork()
    {
        // Arrange
        var first = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 4);
        manager.HostSubRoom(creator, SubRoomPurpose.Voice, null, false);

        // Act
        manager.RemoveSubRoom(first.Id);
        var countAfterRemove = manager.GetRoomSubRooms().Count;
        manager.ClearRoomSubRooms();

        // Assert
        countAfterRemove.Should().Be(1);
        manager.GetRoomSubRooms().Should().BeEmpty();
    }

    /// <summary>
    /// RestoreState сбрасывает активности и удаляет голосовые/экранные подкомнаты
    /// </summary>
    [Fact]
    public void RestoreStateShouldResetActivitiesAndRemoveTransient()
    {
        // Arrange
        var activity = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 4, includeCreator: true);
        var voice = manager.HostSubRoom(creator, SubRoomPurpose.Voice, includeCreator: true);
        var screen = manager.HostSubRoom(creator, SubRoomPurpose.ScreenDemo, includeCreator: true);
        var snapshot = manager.CreateSnapshot();
        manager.ClearRoomSubRooms();

        // Act
        manager.RestoreState(snapshot);
        var restored = manager.GetRoomSubRooms();

        // Assert
        restored.Should().ContainSingle();
        restored[0].Id.Should().Be(activity.Id);
        restored[0].IsActive.Should().BeFalse();
        restored[0].Participants.Should().BeEmpty();
        manager.GetSubRoom(voice.Id).Should().BeNull();
        manager.GetSubRoom(screen.Id).Should().BeNull();
        var next = manager.HostSubRoom(creator, SubRoomPurpose.Activity, 4, false);
        next.Id.Should().Be(snapshot.NextId);
    }
}
