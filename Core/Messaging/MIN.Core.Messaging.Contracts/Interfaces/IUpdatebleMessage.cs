namespace MIN.Core.Messaging.Contracts.Interfaces;

/// <summary>
/// Сообщение, способное изменить своё содержимое
/// </summary>
public interface IUpdateableMessage
{
    /// <summary>
    /// Когда сообщение последний раз обновлялось (DateTime.MinValue — никогда)
    /// </summary>
    DateTime UpdatedAt { get; }

    /// <summary>
    /// Было ли уже изменено сообщение
    /// </summary>
    bool IsUpdated { get; set; }

    /// <summary>
    /// Обновить себя из новой версии
    /// </summary>
    void Update(IMessage newer);
}
