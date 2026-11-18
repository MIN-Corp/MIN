using System.Text.Json;
using FluentAssertions;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Core.Transport.UdpSockets.Models;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Serialization.Tests;

/// <summary>
/// Тесты на <see cref="MIN.Core.Serialization.Json.Converters.IEndpointConverter"/>
/// </summary>
public class IEndpointConverterTests
{
    private readonly JsonSerializerOptions options = TestSerializer.Create().SerializerOptions;

    /// <summary>
    /// TcpEndpoint переживает round-trip через интерфейс IEndpoint
    /// </summary>
    [Fact]
    public void TcpEndpointShouldRoundTrip()
    {
        // Arrange
        IEndpoint endpoint = new TcpEndpoint { IpAddress = "10.0.0.1", Port = 1234, Origin = AddressOrigin.Wan };

        // Act
        var json = JsonSerializer.Serialize(endpoint, options);
        var deserialized = JsonSerializer.Deserialize<IEndpoint>(json, options);

        // Assert
        deserialized.Should().BeOfType<TcpEndpoint>();
        var tcp = (TcpEndpoint)deserialized!;
        tcp.IpAddress.Should().Be("10.0.0.1");
        tcp.Port.Should().Be(1234);
        tcp.Origin.Should().Be(AddressOrigin.Wan);
    }

    /// <summary>
    /// UdpEndpoint переживает round-trip через интерфейс IEndpoint
    /// </summary>
    [Fact]
    public void UdpEndpointShouldRoundTrip()
    {
        // Arrange
        IEndpoint endpoint = new UdpEndpoint { IpAddress = "10.0.0.2", Port = 4321, Origin = AddressOrigin.Lan };

        // Act
        var json = JsonSerializer.Serialize(endpoint, options);
        var deserialized = JsonSerializer.Deserialize<IEndpoint>(json, options);

        // Assert
        deserialized.Should().BeOfType<UdpEndpoint>();
        var udp = (UdpEndpoint)deserialized!;
        udp.IpAddress.Should().Be("10.0.0.2");
        udp.Port.Should().Be(4321);
    }

    /// <summary>
    /// JSON без признаков endpoint отклоняется
    /// </summary>
    [Fact]
    public void UnknownEndpointJsonShouldThrowNotSupported()
    {
        // Act
        var act = () => JsonSerializer.Deserialize<IEndpoint>("""{"foo":1}""", options);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// JSON с ipAddress, но без type, трактуется как TCP
    /// </summary>
    [Fact]
    public void EndpointWithoutTypeShouldDeserializeAsTcp()
    {
        // Act
        var deserialized = JsonSerializer.Deserialize<IEndpoint>("""{"ipAddress":"1.2.3.4","port":99}""", options);

        // Assert
        deserialized.Should().BeOfType<TcpEndpoint>();
    }

    /// <summary>
    /// GetAddress и GetOrigin дают строковые представления
    /// </summary>
    [Fact]
    public void EndpointShouldExposeAddressAndOrigin()
    {
        // Arrange
        IEndpoint endpoint = new TcpEndpoint { IpAddress = "1.2.3.4", Port = 99, Origin = AddressOrigin.Lan };

        // Act
        var address = endpoint.GetAddress();
        var origin = endpoint.GetOrigin();

        // Assert
        address.Should().Be("1.2.3.4:99");
        origin.Should().Be("Lan ");
    }
}
