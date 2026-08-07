using System.Text.Json;
using System.Text.Json.Serialization;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Core.Transport.UdpSockets.Models;

namespace MIN.Core.Serialization.Json.Converters;

/// <summary>
/// <see cref="JsonConverter"/> для <see cref="IEndpoint"/>
/// </summary>
public class IEndpointConverter : JsonConverter<IEndpoint>
{
    /// <inheritdoc cref="JsonConverter{T}.Read(ref Utf8JsonReader, Type, JsonSerializerOptions)"/>
    public override IEndpoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.Number
            && type.GetInt32() == (int)TransportType.Udp)
        {
            return JsonSerializer.Deserialize<UdpEndpoint>(root.GetRawText(), options)
                ?? throw new InvalidCastException("Не удалось распарсить UdpEndpoint");
        }

        if (root.TryGetProperty("ipAddress", out _))
        {
            return JsonSerializer.Deserialize<TcpEndpoint>(root.GetRawText(), options)
                ?? throw new InvalidCastException("Не удалось распарсить Endpoint");
        }

        throw new NotSupportedException("Неизвестный тип Endpoint");
    }

    /// <inheritdoc cref="JsonConverter{T}.Write(Utf8JsonWriter, T, JsonSerializerOptions)"/>
    public override void Write(Utf8JsonWriter writer, IEndpoint value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
