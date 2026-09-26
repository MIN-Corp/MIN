using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Messaging.Contracts.Messages;

/// <summary>
/// Базовый класс для сообщений, имеющих текстовое представление и могут быть отредактированы
/// </summary>
public abstract class BaseUpdatebleMessage : BaseMessage, IUpdateableMessage
{
    /// <inheritdoc />
    public bool IsUpdated { get; set; }

    /// <inheritdoc />
    public DateTime UpdatedAt { get; set; }

    /// <inheritdoc />
    public virtual void Update(IMessage newer)
    {
        IsUpdated = true;
        UpdatedAt = DateTime.Now;
    }
}
