namespace MIN.Core.Messaging.Contracts.Interfaces;

/// <summary>
/// Сообщение, у которого есть автор-участник с именем,
/// которое нужно поддерживать актуальным при смене имени
/// </summary>
public interface IAuthoredMessage : IMessage
{
    /// <summary>
    /// Имя отправителя сообщения
    /// </summary>
    string SenderName { get; set; }
}
