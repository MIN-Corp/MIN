using System.Text.Json;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Serialization.Contracts.Interfaces;

/// <summary>
/// Сериализует и десериализует сообщения
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Настройки сериализации
    /// </summary>
    JsonSerializerOptions SerializerOptions { get; }

    /// <summary>
    /// Сериализует сообщение в массив байтов
    /// </summary>
    byte[] Serialize(IMessage message);

    /// <summary>
    /// Десериализует массив байтов в сообщение
    /// </summary>
    IMessage Deserialize(byte[] data);
}
