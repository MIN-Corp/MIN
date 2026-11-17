using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Core.Entities;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models.Persistence;
using MIN.Core.Stores.Services.Persistence;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="MessageMigrationService"/>
/// </summary>
public class MessageMigrationServiceTests
{
    private sealed class FakeMigrator : IMessageMigrator
    {
        private readonly Func<IMessage, RoomMigrationContext, bool> migrate;

        public FakeMigrator(Type messageType, Func<IMessage, RoomMigrationContext, bool> migrate)
        {
            MessageType = messageType;
            this.migrate = migrate;
        }

        public Type MessageType { get; }

        public bool Migrate(IMessage message, RoomMigrationContext context) => migrate(message, context);
    }

    /// <summary>
    /// Миграция считает восстановленные сообщения
    /// </summary>
    [Fact]
    public void ApplyShouldCountRepaired()
    {
        // Arrange
        var logger = new TestLoggerProvider();
        var service = new MessageMigrationService([new FakeMigrator(typeof(ChatTextMessage), (_, _) => true)], logger);
        var messages = new List<IMessage> { new ChatTextMessage(), new ChatTextMessage() };

        // Act
        var repaired = ((IMessageMigrationService)service).Apply(messages, new Room());

        // Assert
        repaired.Should().Be(2);
    }

    /// <summary>
    /// Упавший мигратор логируется и не ломает остальные
    /// </summary>
    [Fact]
    public void MigratorExceptionShouldBeLoggedAndSkipped()
    {
        // Arrange
        var logger = new TestLoggerProvider();
        var service = new MessageMigrationService(
            [new FakeMigrator(typeof(ChatTextMessage), (_, _) => throw new InvalidOperationException("бум"))],
            logger);
        var messages = new List<IMessage> { new ChatTextMessage() };

        // Act
        var repaired = ((IMessageMigrationService)service).Apply(messages, new Room());

        // Assert
        repaired.Should().Be(0);
        logger.Items.Should().Contain(i => i.Message.Contains("бум"));
    }

    /// <summary>
    /// Без миграторов для типа сообщения — ноль
    /// </summary>
    [Fact]
    public void NoMigratorShouldReturnZero()
    {
        // Arrange
        var service = new MessageMigrationService([], new TestLoggerProvider());
        var messages = new List<IMessage> { new ChatTextMessage() };

        // Act
        var repaired = ((IMessageMigrationService)service).Apply(messages, new Room());

        // Assert
        repaired.Should().Be(0);
    }
}
