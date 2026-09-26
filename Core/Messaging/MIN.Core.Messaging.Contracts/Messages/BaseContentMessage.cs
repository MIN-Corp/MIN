using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Messaging.Contracts.Messages;

/// <summary>
/// Базовый класс для сообщений, имеющих текстовое представление и могут быть отредактированы
/// </summary>
public abstract class BaseContentMessage : BaseUpdatebleMessage, IContentEditable
{
    /// <inheritdoc />
    public string Content { get; set; } = string.Empty;

    /// <inheritdoc />
    public override void Update(IMessage newer)
    {
        if (newer is IContentEditable newContent)
        {
            Content = newContent.Content;
            base.Update(newer);
        }
    }
}
