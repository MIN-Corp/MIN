using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Core.Transport.UdpSockets.Models;
using MIN.Discovery.Messaging;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Serialization.Tests;

/// <summary>
/// Тесты на <see cref="MIN.Core.Serialization.Json.JsonMessageSerializer"/>
/// </summary>
public class JsonMessageSerializerTests
{
    /// <summary>
    /// Данные для теории: все известные типы сообщений
    /// </summary>
    public static IEnumerable<object[]> AllMessageData()
        => TestSerializer.AllMessages.Select(message => new object[] { message });

    /// <summary>
    /// Каждое сообщение переживает сериализацию и десериализацию с сохранением ключевых полей
    /// </summary>
    [Theory]
    [MemberData(nameof(AllMessageData))]
    public void AllMessagesShouldRoundTrip(IMessage message)
    {
        // Arrange
        var serializer = TestSerializer.Create();
        message.SenderId = Guid.NewGuid();
        PopulateRequiredProperties(message);

        // Act
        var bytes = serializer.Serialize(message);
        var deserialized = serializer.Deserialize(bytes);

        // Assert
        deserialized.GetType().Should().Be(message.GetType());
        deserialized.Id.Should().Be(message.Id);
        deserialized.TypeTag.Should().Be(message.TypeTag);
        deserialized.SenderId.Should().Be(message.SenderId);
        deserialized.Timestamp.Should().Be(message.Timestamp);
        deserialized.RecipientId.Should().Be(message.RecipientId);
        if (message is IContentEditable content && deserialized is IContentEditable deserializedContent)
        {
            deserializedContent.Content.Should().Be(content.Content);
        }
    }

    /// <summary>
    /// Сериализация использует camelCase
    /// </summary>
    [Fact]
    public void SerializeShouldUseCamelCase()
    {
        // Arrange
        var serializer = TestSerializer.Create();
        var message = new ChatTextMessage { Content = "привет", SenderName = "Автор" };

        // Act
        var json = Encoding.UTF8.GetString(serializer.Serialize(message));

        // Assert
        json.Should().Contain("\"typeTag\"");
        json.Should().Contain("\"senderId\"");
        json.Should().Contain("\"senderName\"");
        json.Should().NotContain("\"TypeTag\"");
    }

    /// <summary>
    /// JSON без typeTag отклоняется
    /// </summary>
    [Fact]
    public void DeserializeMissingTypeTagShouldThrowInvalidOperation()
    {
        // Arrange
        var serializer = TestSerializer.Create();
        var bytes = """{"content":"привет"}"""u8.ToArray();

        // Act
        var act = () => serializer.Deserialize(bytes);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*TypeTag*");
    }

    /// <summary>
    /// Незарегистрированный тег отклоняется с NotSupportedException
    /// </summary>
    [Fact]
    public void DeserializeUnknownTypeTagShouldThrowNotSupported()
    {
        // Arrange
        var serializer = TestSerializer.Create(new ChatTextMessage());
        var bytes = """{"typeTag":250}"""u8.ToArray();

        // Act
        var act = () => serializer.Deserialize(bytes);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// Тег вне диапазона byte отклоняется с NotSupportedException
    /// </summary>
    [Fact]
    public void DeserializeOutOfRangeTypeTagShouldThrowNotSupported()
    {
        // Arrange
        var serializer = TestSerializer.Create(new ChatTextMessage());
        var bytes = """{"typeTag":300}"""u8.ToArray();

        // Act
        var act = () => serializer.Deserialize(bytes);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// Строковый typeTag отклоняется с NotSupportedException
    /// </summary>
    [Fact]
    public void DeserializeStringTypeTagShouldThrowNotSupported()
    {
        // Arrange
        var serializer = TestSerializer.Create(new ChatTextMessage());
        var bytes = """{"typeTag":"ChatTextMessage"}"""u8.ToArray();

        // Act
        var act = () => serializer.Deserialize(bytes);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// Битый JSON отклоняется
    /// </summary>
    [Fact]
    public void DeserializeMalformedJsonShouldThrow()
    {
        // Arrange
        var serializer = TestSerializer.Create();

        // Act
        var act = () => serializer.Deserialize("{не json"u8.ToArray());

        // Assert
        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// Регистрация двух сообщений с одним тегом отклоняется
    /// </summary>
    [Fact]
    public void DuplicateTagRegistrationShouldThrow()
    {
        // Act
        var act = () => TestSerializer.Create(new ChatTextMessage(), new DuplicateTagMessage());

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
    }

    /// <summary>
    /// Вложенные IEndpoint (TCP и UDP) переживают round-trip с сохранением типа
    /// </summary>
    [Fact]
    public void NestedEndpointsShouldRoundTrip()
    {
        // Arrange
        var serializer = TestSerializer.Create();
        var message = new DiscoveryResponseMessage
        {
            RoomDiscoveryInfos =
            [
                new MIN.Discovery.Services.Contracts.Models.RoomDiscoveryInfo
                {
                    Room = new RoomInfo { Name = "Комната" },
                    Endpoints =
                    [
                        new TcpEndpoint { IpAddress = "192.168.1.10", Port = 5000, Origin = AddressOrigin.Lan },
                        new UdpEndpoint { IpAddress = "192.168.1.10", Port = 5001, Origin = AddressOrigin.Vpn },
                    ],
                }
            ],
        };

        // Act
        var deserialized = (DiscoveryResponseMessage)serializer.Deserialize(serializer.Serialize(message));

        // Assert
        var endpoints = deserialized.RoomDiscoveryInfos[0].Endpoints.ToList();
        endpoints.Should().HaveCount(2);
        endpoints[0].Should().BeOfType<TcpEndpoint>();
        endpoints[1].Should().BeOfType<UdpEndpoint>();
        var tcp = (TcpEndpoint)endpoints[0];
        tcp.IpAddress.Should().Be("192.168.1.10");
        tcp.Port.Should().Be(5000);
        tcp.Origin.Should().Be(AddressOrigin.Lan);
        var udp = (UdpEndpoint)endpoints[1];
        udp.Port.Should().Be(5001);
        udp.Origin.Should().Be(AddressOrigin.Vpn);
    }

    /// <summary>
    /// Вложенные IMessage переживают round-trip с сохранением типов
    /// </summary>
    [Fact]
    public void NestedMessagesShouldRoundTrip()
    {
        // Arrange
        var serializer = TestSerializer.Create();
        var message = new Messaging.Stateless.RoomRelated.History.ChatHistoryResponseMessage
        {
            Messages =
            [
                new ChatTextMessage { Content = "первое", SenderName = "А" },
                new ChatTextMessage { Content = "второе", SenderName = "Б" },
            ],
            TotalCount = 2,
        };

        // Act
        var deserialized = (Messaging.Stateless.RoomRelated.History.ChatHistoryResponseMessage)serializer.Deserialize(serializer.Serialize(message));

        // Assert
        deserialized.Messages.Should().HaveCount(2);
        deserialized.Messages.Should().AllBeOfType<ChatTextMessage>();
        ((ChatTextMessage)deserialized.Messages[0]).Content.Should().Be("первое");
        ((ChatTextMessage)deserialized.Messages[1]).Content.Should().Be("второе");
        deserialized.TotalCount.Should().Be(2);
    }

    /// <summary>
    /// Сообщение без получателя публичное, recipientId в JSON отсутствует
    /// </summary>
    [Fact]
    public void MessageWithoutRecipientShouldBePublic()
    {
        // Arrange
        var serializer = TestSerializer.Create();
        var message = new ChatTextMessage { Content = "всем" };

        // Act
        var json = Encoding.UTF8.GetString(serializer.Serialize(message));
        var deserialized = serializer.Deserialize(serializer.Serialize(message));

        // Assert
        message.IsPublic.Should().BeTrue();
        json.Should().NotContain("recipientId");
        deserialized.IsPublic.Should().BeTrue();
    }

    /// <summary>
    /// Приватное сообщение сохраняет получателя
    /// </summary>
    [Fact]
    public void PrivateMessageShouldKeepRecipient()
    {
        // Arrange
        var serializer = TestSerializer.Create();
        var recipientId = Guid.NewGuid();
        var message = new ChatTextMessage { Content = "лично", RecipientId = recipientId };

        // Act
        var deserialized = (ChatTextMessage)serializer.Deserialize(serializer.Serialize(message));

        // Assert
        deserialized.RecipientId.Should().Be(recipientId);
        deserialized.IsPublic.Should().BeFalse();
    }

    /// <summary>
    /// Заполняет required-свойства сообщения реалистичными значениями (Activator.CreateInstance обходит проверку required)
    /// </summary>
    private static void PopulateRequiredProperties(IMessage message)
    {
        foreach (var property in message.GetType().GetProperties())
        {
            if (property.GetCustomAttribute<RequiredMemberAttribute>() == null || property.GetValue(message) != null)
            {
                continue;
            }

            property.SetValue(message, CreateDefaultValue(property.PropertyType));
        }
    }

    /// <summary>
    /// Создаёт значение по умолчанию для типа свойства
    /// </summary>
    private static object? CreateDefaultValue(Type type)
    {
        if (type == typeof(string))
        {
            return "test";
        }

        if (type == typeof(Version))
        {
            return new Version(1, 0, 0, 0);
        }

        if (type == typeof(byte[]))
        {
            return new byte[] { 1, 2, 3 };
        }

        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (type == typeof(Participant))
        {
            return new Participant { Name = "Участник" };
        }

        if (type == typeof(ParticipantInfo))
        {
            return new ParticipantInfo { Id = Guid.NewGuid(), Name = "Участник" };
        }

        if (type == typeof(RoomInfo))
        {
            return new RoomInfo { Name = "Комната" };
        }

        if (type == typeof(IMessage))
        {
            return new ChatTextMessage { Content = "вложенное" };
        }

        return type == typeof(IEnumerable<IEndpoint>) ? new List<IEndpoint> { new TcpEndpoint { IpAddress = "127.0.0.1", Port = 1 } } : Activator.CreateInstance(type);
    }
}
