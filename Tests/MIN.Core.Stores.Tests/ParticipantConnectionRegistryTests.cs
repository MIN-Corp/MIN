using FluentAssertions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Exceptions;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Registries;
using MIN.Core.Stores.Contracts.Registries.Models;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="ParticipantConnectionRegistry"/>
/// </summary>
public class ParticipantConnectionRegistryTests
{
    private readonly IParticipantConnectionRegistry registry = new ParticipantConnectionRegistry();

    /// <summary>
    /// Регистрация связывает соединение и участника в обе стороны
    /// </summary>
    [Fact]
    public void RegisterShouldLinkBothDirections()
    {
        // Arrange
        var connectionId = Guid.NewGuid();
        var participant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Участник" };

        // Act
        registry.Register(connectionId, participant);

        // Assert
        registry.ConnectionExists(connectionId).Should().BeTrue();
        registry.GetParticipant(connectionId).Should().BeSameAs(participant);
        registry.GetParticipantIdFromConnectionId(connectionId).Should().Be(participant.Id);
        registry.GetConnectionIdFromParticipantId(participant.Id).Should().Be(connectionId);
        registry.TryGetParticipantFromConnectionId(connectionId, out var tried).Should().BeTrue();
        tried.Should().BeSameAs(participant);
        registry.TryGetConnectionIdFromParticipantId(participant.Id, out var triedConnection).Should().BeTrue();
        triedConnection.Should().Be(connectionId);
    }

    /// <summary>
    /// Неизвестное соединение бросает ConnectionNotRegistredException
    /// </summary>
    [Fact]
    public void UnknownConnectionShouldThrow()
    {
        // Act
        var getParticipant = () => registry.GetParticipant(Guid.NewGuid());
        var getParticipantId = () => registry.GetParticipantIdFromConnectionId(Guid.NewGuid());
        var getConnectionId = () => registry.GetConnectionIdFromParticipantId(Guid.NewGuid());

        // Assert
        getParticipant.Should().Throw<ConnectionNotRegistredException>();
        getParticipantId.Should().Throw<ConnectionNotRegistredException>();
        getConnectionId.Should().Throw<ParticipantNotRegistredException>();
        registry.ConnectionExists(Guid.NewGuid()).Should().BeFalse();
        registry.TryGetParticipantFromConnectionId(Guid.NewGuid(), out _).Should().BeFalse();
    }

    /// <summary>
    /// Локальный участник регистрируется под LocalConnectionId
    /// </summary>
    [Fact]
    public void RegisterLocalParticipantShouldUseLocalConnectionId()
    {
        // Arrange
        var participant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Я" };

        // Act
        registry.RegisterLocalParticipant(participant);

        // Assert
        registry.GetParticipant(CoreRegistryConstants.LocalConnectionId).Should().BeSameAs(participant);
        registry.GetConnectionIdFromParticipantId(participant.Id).Should().Be(CoreRegistryConstants.LocalConnectionId);
    }

    /// <summary>
    /// Unregister разрывает обе связи
    /// </summary>
    [Fact]
    public void UnregisterShouldRemoveBothLinks()
    {
        // Arrange
        var connectionId = Guid.NewGuid();
        var participant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Участник" };
        registry.Register(connectionId, participant);

        // Act
        registry.Unregister(connectionId);

        // Assert
        registry.ConnectionExists(connectionId).Should().BeFalse();
        registry.TryGetConnectionIdFromParticipantId(participant.Id, out _).Should().BeFalse();
    }

    /// <summary>
    /// UnregisterAllExceptLocal удаляет все соединения кроме локального
    /// </summary>
    [Fact]
    public void UnregisterAllExceptLocalShouldKeepLocal()
    {
        // Arrange
        var local = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Я" };
        var remote = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Удалённый" };
        registry.RegisterLocalParticipant(local);
        registry.Register(Guid.NewGuid(), remote);

        // Act
        var unregistered = registry.UnregisterAllExceptLocal().ToList();

        // Assert
        unregistered.Should().HaveCount(2);
        registry.ConnectionExists(CoreRegistryConstants.LocalConnectionId).Should().BeTrue();
        registry.TryGetConnectionIdFromParticipantId(remote.Id, out _).Should().BeFalse();
    }

    /// <summary>
    /// Перерегистрация участника на новое соединение обновляет обратную связь
    /// </summary>
    [Fact]
    public void ReregisterShouldUpdateReverseLink()
    {
        // Arrange
        var participant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Участник" };
        var oldConnection = Guid.NewGuid();
        var newConnection = Guid.NewGuid();
        registry.Register(oldConnection, participant);

        // Act
        registry.Register(newConnection, participant);

        // Assert
        registry.GetConnectionIdFromParticipantId(participant.Id).Should().Be(newConnection);
        registry.ConnectionExists(oldConnection).Should().BeTrue();
    }
}
