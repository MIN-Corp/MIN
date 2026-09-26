namespace MIN.Core.Messaging.Contracts.Interfaces;

/// <summary>
/// Сообщение, которое имеет текстовое поле и может быть изменено
/// </summary>
public interface IContentEditable : IUpdateableMessage
{
    /// <summary>
    /// Содержимое сообщения
    /// </summary>
    string Content { get; set; }
}
