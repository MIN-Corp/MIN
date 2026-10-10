using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models.Persistence;

namespace MIN.Core.Stores.Contracts.Interfaces;

/// <summary>
/// Мигратор сообщения, при потере данных из-за обновлений, который восстановит данные 1 типа сообщения
/// </summary>
/// <remarks>
/// Idempotent — runs on every load; second run must be a no-op (check "is it actually broken?" first, e.g. string.IsNullOrEmpty(SenderName)) <br/> <br/>
/// 
/// Bounded repair — derive from data in the snapshot; never guess permanent values <br/> <br/>
/// 
/// Exceptions in a migrator never fail the room(orchestrator isolates) <br/> <br/>
/// 
/// If a change is lossy and unrecoverable → don't write a migrator; that's a SchemaVersion bump + .bak-and-skip, by design <br/> <br/>
/// 
/// Cross - message migrations(e.g.rebuilding ReplyToMessageDescription from the referenced message) are out of scope for v1 — the context is per-message + room;  <br/> <br/>
/// 
/// can add a second pass type later if needed
/// </remarks>
public interface IMessageMigrator
{
    /// <summary>Тип сообщения, который мигратор чинит</summary>
    Type MessageType { get; }

    /// <summary>
    /// Починить сообщение из данных снимка. Должен быть идемпотентным.
    /// </summary>
    /// <returns>true, если сообщение было изменено</returns>
    bool Migrate(IMessage message, RoomMigrationContext context);
}
