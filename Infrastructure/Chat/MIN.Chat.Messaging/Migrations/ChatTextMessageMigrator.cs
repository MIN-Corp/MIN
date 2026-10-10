using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models.Persistence;

namespace MIN.Chat.Messaging.Migrations;

/// <summary>
/// <inheritdoc cref="IMessageMigrator"/> для <see cref="ChatTextMessage"/>
/// </summary>
public sealed class ChatTextMessageMigrator : IMessageMigrator
{
    Type IMessageMigrator.MessageType => typeof(ChatTextMessage);

    bool IMessageMigrator.Migrate(IMessage message, RoomMigrationContext context)
    {
        if (message is not ChatTextMessage { SenderName: "" or null } text)
        {
            return false;
        }

        var sender = context.Room.CurrentParticipants.FirstOrDefault(p => p.Id == text.SenderId);
        if (sender == null)
        {
            return false;
        }

        text.SenderName = sender.Name;
        return true;
    }
}
