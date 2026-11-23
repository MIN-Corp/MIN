using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Messaging.Contracts.Messages;

/// <summary>
/// Базовый класс для сообщений, имеющих текстовое представление и могут быть отредактированы
/// </summary>
public abstract class BaseUpdatableMessage : BaseMessage, IUpdatableMessage
{
    /// <inheritdoc />
    public bool IsUpdated { get; protected set; }

    /// <inheritdoc />
    public DateTime UpdatedAt { get; protected set; }

    /// <inheritdoc />
    public virtual void Update(IMessage newer)
    {
        IsUpdated = true;
        UpdatedAt = DateTime.Now;
    }
}
