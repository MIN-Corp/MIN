namespace MIN.Core.Messaging.Contracts.Interfaces;

/// <summary>
/// Сообщение с чувствительными полями
/// </summary>
public interface IMessageWithSecuredFields
{
    /// <summary>
    /// Возвращает копию с очищенными чувствительными полями; исходный объект не изменяется
    /// </summary>
    IMessage Sanitize();
}
