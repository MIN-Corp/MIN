using MIN.Core.Entities;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models.Persistence;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Stores.Services.Persistence;

/// <inheritdoc cref="IMessageMigrationService"/>
public sealed class MessageMigrationService : IMessageMigrationService
{
    private readonly ILoggerProvider logger;
    private readonly ILookup<Type, IMessageMigrator> messageMigrators;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MessageMigrationService"/>
    /// </summary>
    public MessageMigrationService(IEnumerable<IMessageMigrator> messageMigrators,
        ILoggerProvider logger)
    {
        this.messageMigrators = messageMigrators.ToLookup(x => x.MessageType);
        this.logger = logger;
    }

    int IMessageMigrationService.Apply(List<IMessage> messages, Room room)
    {
        var context = new RoomMigrationContext { Room = room };
        var repairedCount = 0;

        foreach (var message in messages)
        {
            var migrators = messageMigrators[message.GetType()];
            if (!migrators.Any())
            {
                continue;
            }

            foreach (var migrator in migrators)
            {
                try
                {
                    if (migrator.Migrate(message, context))
                    {
                        repairedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.Log($"Мигратор '{migrator.GetType().Name}' упал на сообщении {message.Id}: {ex.Message}",
                        LogLevel.Warning);
                }
            }
        }

        return repairedCount;
    }
}
