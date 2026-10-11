using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Serialization.Contracts.Interfaces;
using MIN.Core.Serialization.Json.Converters;

namespace MIN.Core.Serialization.Json;

/// <summary>
/// Реализация сериализатора на основе Json
/// </summary>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private readonly IEnumerable<IMessage> messageTypes;
    private readonly ConcurrentDictionary<MessageTypeTag, Func<byte[], IMessage>> deserializers = new();
    private readonly JsonSerializerOptions serializerOptions;

    JsonSerializerOptions IMessageSerializer.SerializerOptions => serializerOptions;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="JsonMessageSerializer"/>
    /// </summary>
    public JsonMessageSerializer(IEnumerable<IMessage> messageTypes)
    {
        this.messageTypes = messageTypes;
        serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
                {
                    new IEndpointConverter(),
                    new IMessageConverter(this),
                }
        };
        InitializeDeserializers();
    }

    private void InitializeDeserializers()
    {
        foreach (var type in messageTypes)
        {
            var messageType = type.GetType();
            var instance = (IMessage)Activator.CreateInstance(messageType)!;
            var tag = instance.TypeTag;
            var deserializer = CreateDeserializer(messageType);
            if (!deserializers.TryAdd(tag, deserializer))
            {
                throw new InvalidOperationException($"Deserializer for tag {tag} already registered");
            }
        }
    }

    byte[] IMessageSerializer.Serialize(IMessage message)
        => JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), serializerOptions);

    IMessage IMessageSerializer.Deserialize(byte[] data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        if (!root.TryGetProperty("typeTag", out var typeTagElement) && !root.TryGetProperty("TypeTag", out typeTagElement))
        {
            throw new InvalidOperationException("Missing TypeTag property in message JSON");
        }

        var typeTag = (MessageTypeTag)typeTagElement.GetByte();
        deserializers.TryGetValue(typeTag, out var deserializer);

        return deserializer == null
            ? throw new NotSupportedException($"No deserializer registered for message type {typeTag}")
            : deserializer(data);
    }

    private Func<byte[], IMessage> CreateDeserializer(Type messageType)
        => data => (IMessage)JsonSerializer.Deserialize(data, messageType, serializerOptions)!;
}
