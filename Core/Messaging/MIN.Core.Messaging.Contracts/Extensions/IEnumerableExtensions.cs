using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Messaging.Contracts.Extensions
{
    /// <summary>
    /// Расширения для <see cref="IEnumerable{IMessage}"/>
    /// </summary>
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Очистить сообщения, которые не могут быть увидены определённым участником
        /// </summary>
        public static IEnumerable<IMessage> SanitizeMessagesForParticipant(this IEnumerable<IMessage> messages, Guid participantId)
            => messages
                .Where(x => x.IsPublic || x.RecipientId == participantId || x.SenderId == participantId)
                .ToList()
                .Select(x => x is IMessageWithSecuredFields messageWithSecuredFields ? messageWithSecuredFields.Sanitize() : x);
    }
}
