using System.Reflection;
using FluentAssertions;
using Xunit;

namespace MIN.Discovery.Tests;

/// <summary>
/// Тесты на UdpPacketHelper (internal — через рефлексию)
/// </summary>
public class UdpPacketHelperTests
{
    private readonly static Type helperType = typeof(Transport.UdpBroadcast.UdpBroadcastDiscoveryTransport).Assembly
        .GetType("MIN.Discovery.Transport.UdpBroadcast.Helpers.UdpPacketHelper")!;

    private static byte[] Pack(byte[] payload)
        => (byte[])helperType.GetMethod("Pack")!.Invoke(null, [payload])!;

    private static bool TryUnpack(byte[] packet, out byte[] payload)
    {
        var args = new object?[] { packet, null };
        var result = (bool)helperType.GetMethod("TryUnpack")!.Invoke(null, args)!;
        payload = (byte[])args[1]!;
        return result;
    }

    /// <summary>
    /// Упаковка и распаковка возвращают исходные байты
    /// </summary>
    [Fact]
    public void PackUnpackShouldRoundTrip()
    {
        // Arrange
        var payload = Enumerable.Range(0, 500).Select(i => (byte)(i % 256)).ToArray();

        // Act
        var packet = Pack(payload);
        var ok = TryUnpack(packet, out var unpacked);

        // Assert
        ok.Should().BeTrue();
        unpacked.Should().Equal(payload);
        packet.Length.Should().Be(payload.Length + 4);
    }

    /// <summary>
    /// Пустой payload упаковывается и распаковывается
    /// </summary>
    [Fact]
    public void EmptyPayloadShouldRoundTrip()
    {
        // Act
        var ok = TryUnpack(Pack([]), out var unpacked);

        // Assert
        ok.Should().BeTrue();
        unpacked.Should().BeEmpty();
    }

    /// <summary>
    /// Неверная сигнатура отклоняется
    /// </summary>
    [Fact]
    public void WrongMagicShouldFail()
    {
        // Arrange
        var packet = Pack([1, 2, 3]);
        packet[0] = (byte)'X';

        // Act
        var ok = TryUnpack(packet, out _);

        // Assert
        ok.Should().BeFalse();
    }

    /// <summary>
    /// Укороченный пакет отклоняется
    /// </summary>
    [Fact]
    public void TruncatedPacketShouldFail()
    {
        // Arrange
        var packet = Pack([1, 2, 3]);

        // Act
        var ok = TryUnpack(packet[..^1], out _);

        // Assert
        ok.Should().BeFalse();
    }

    /// <summary>
    /// Пакет короче заголовка отклоняется
    /// </summary>
    [Fact]
    public void TooShortPacketShouldFail()
    {
        // Act
        var ok = TryUnpack([1, 2], out _);

        // Assert
        ok.Should().BeFalse();
    }

    /// <summary>
    /// Длина в заголовке не совпадает с фактической — отказ
    /// </summary>
    [Fact]
    public void LengthMismatchShouldFail()
    {
        // Arrange
        var packet = Pack([1, 2, 3]);
        packet[3] = 99;

        // Act
        var ok = TryUnpack(packet, out _);

        // Assert
        ok.Should().BeFalse();
    }

    /// <summary>
    /// Payload больше UDP-лимита отклоняется
    /// </summary>
    [Fact]
    public void OversizedPayloadShouldThrow()
    {
        // Arrange
        var payload = new byte[1469];

        // Act
        var act = () => Pack(payload);

        // Assert
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
    }

    /// <summary>
    /// Payload ровно на границе лимита упаковывается
    /// </summary>
    [Fact]
    public void MaxSizePayloadShouldPack()
    {
        // Arrange
        var payload = new byte[1468];

        // Act
        var ok = TryUnpack(Pack(payload), out var unpacked);

        // Assert
        ok.Should().BeTrue();
        unpacked.Should().HaveCount(1468);
    }
}
