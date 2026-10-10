using MIN.Core.Entities;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Stores.Contracts.Interfaces;

/// <summary>
/// Менеджер по миграциям сообщений
/// </summary>
public interface IMessageMigrationService
{
    /// <summary>
    /// Выполнить миграцию
    /// </summary>
    int Apply(List<IMessage> messages, Room room);
}
