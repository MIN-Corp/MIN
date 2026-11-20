using FluentAssertions;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Services;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="ParticipantStore"/>
/// </summary>
public class ParticipantStoreTests
{
    private readonly IParticipantStore store = new ParticipantStore();

    /// <summary>
    /// Добавление, поиск и количество работают
    /// </summary>
    [Fact]
    public void AddAndGetShouldWork()
    {
        // Arrange
        var participant = new Participant { Name = "Участник" };

        // Act
        store.AddParticipant(participant);
        var found = store.GetParticipantById(participant.Id);
        var tryFound = store.TryGetParticipantById(participant.Id, out var tried);
        var tryMissing = store.TryGetParticipantById(Guid.NewGuid(), out _);

        // Assert
        found.Should().BeSameAs(participant);
        tryFound.Should().BeTrue();
        tried.Should().BeSameAs(participant);
        tryMissing.Should().BeFalse();
        store.GetParticipants().Should().HaveCount(1);
    }

    /// <summary>
    /// Повторное добавление того же участника не создаёт дубль
    /// </summary>
    [Fact]
    public void AddDuplicateShouldBeIgnored()
    {
        // Arrange
        var participant = new Participant { Name = "Участник" };

        // Act
        store.AddParticipant(participant);
        store.AddParticipant(new Participant { Id = participant.Id, Name = "Дубль" });

        // Assert
        store.GetParticipants().Should().HaveCount(1);
        store.GetParticipantById(participant.Id).Name.Should().Be("Участник");
    }

    /// <summary>
    /// Неизвестный участник: GetParticipantById бросает ArgumentNullException (текущее поведение)
    /// </summary>
    [Fact]
    public void GetParticipantByIdUnknownShouldThrow()
    {
        // Act
        var act = () => store.GetParticipantById(Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Обновление заменяет данные и проставляет UpdatedAt
    /// </summary>
    [Fact]
    public void UpdateParticipantShouldReplaceData()
    {
        // Arrange
        var participant = new Participant { Name = "Старое имя" };
        store.AddParticipant(participant);

        // Act
        store.UpdateParticipant(participant.Id, new Participant { Id = participant.Id, Name = "Новое имя" });

        // Assert
        var updated = store.GetParticipantById(participant.Id);
        updated.Name.Should().Be("Новое имя");
        updated.UpdatedAt.Should().NotBeNull();
    }

    /// <summary>
    /// Удаление и очистка работают
    /// </summary>
    [Fact]
    public void RemoveAndClearShouldWork()
    {
        // Arrange
        var participant = new Participant { Name = "Участник" };
        store.AddParticipant(participant);

        // Act
        store.RemoveParticipant(participant.Id);
        var countAfterRemove = store.GetParticipants().Count();
        store.AddParticipant(participant);
        store.ClearParticipants();

        // Assert
        countAfterRemove.Should().Be(0);
        store.GetParticipants().Should().BeEmpty();
    }

    /// <summary>
    /// Bind заменяет список участников целиком
    /// </summary>
    [Fact]
    public void BindShouldReplaceList()
    {
        // Arrange
        store.AddParticipant(new Participant { Name = "Старый" });
        var newList = new List<Participant> { new() { Name = "Новый 1" }, new() { Name = "Новый 2" } };

        // Act
        store.Bind(newList);

        // Assert
        store.GetParticipants().Should().HaveCount(2);
        store.GetParticipants().Select(p => p.Name).Should().Equal("Новый 1", "Новый 2");
    }

    /// <summary>
    /// GetParticipantByIds возвращает снапшот, не зависящий от последующих мутаций
    /// </summary>
    [Fact]
    public void GetParticipantByIdsShouldReturnSnapshot()
    {
        // Arrange
        var first = new Participant { Name = "Первый" };
        var late = new Participant { Name = "Поздний" };
        store.AddParticipant(first);
        var requestedIds = new[] { first.Id, late.Id };

        // Act
        var enumerable = store.GetParticipantByIds(requestedIds);
        store.AddParticipant(late);
        var result = enumerable.ToList();

        // Assert
        result.Should().ContainSingle().Which.Should().BeSameAs(first);
    }

    /// <summary>
    /// MarkAllParticipantsOffline переводит всех в офлайн кроме исключённого
    /// </summary>
    [Fact]
    public void MarkAllOfflineShouldExceptOne()
    {
        // Arrange
        var first = new Participant { Name = "Первый", CurrentStatus = OnlineStatus.Online };
        var second = new Participant { Name = "Второй", CurrentStatus = OnlineStatus.Online };
        store.AddParticipant(first);
        store.AddParticipant(second);

        // Act
        store.MarkAllParticipantsOffline(first.Id);

        // Assert
        first.CurrentStatus.Should().Be(OnlineStatus.Online);
        second.CurrentStatus.Should().Be(OnlineStatus.Offline);
    }

    /// <summary>
    /// Параллельные добавления не теряют участников
    /// </summary>
    [Fact]
    public void ConcurrentAddsShouldNotLoseParticipants()
    {
        // Act
        Parallel.For(0, 10, batch =>
        {
            for (var i = 0; i < 100; i++)
            {
                store.AddParticipant(new Participant { Name = $"{batch}-{i}" });
            }
        });

        // Assert
        store.GetParticipants().Should().HaveCount(1000);
    }
}
