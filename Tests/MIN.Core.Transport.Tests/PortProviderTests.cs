using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Helpers;
using MIN.Tests.Common;
using Xunit;
using TransportType = MIN.Core.Transport.Contracts.Enum.TransportType;

namespace MIN.Core.Transport.Tests;

/// <summary>
/// Тесты на <see cref="PortProvider"/>
/// </summary>
public class PortProviderTests
{
    /// <summary>
    /// Без предпочтений выделяется порт из динамического диапазона
    /// </summary>
    [Fact]
    public void AllocatePortShouldReturnDynamicRangePort()
    {
        // Act
        var port = PortProvider.AllocatePort(TransportType.Tcp);

        // Assert
        port.Should().BeInRange(49152, 65535);
        PortProvider.ReleasePort(port, TransportType.Tcp);
    }

    /// <summary>
    /// Свободный предпочтительный порт выделяется как есть
    /// </summary>
    [Fact]
    public void AllocatePortShouldRespectFreePreferredPort()
    {
        // Arrange
        var preferred = (ushort)TestPorts.GetFreeTcpPort();

        // Act
        var port = PortProvider.AllocatePort(TransportType.Tcp, preferred, 1);

        // Assert
        port.Should().Be(preferred);
        PortProvider.ReleasePort(port, TransportType.Tcp);
    }

    /// <summary>
    /// Занятый предпочтительный порт пропускается, берётся следующий
    /// </summary>
    [Fact]
    public void AllocatePortShouldSkipBusyPreferredPort()
    {
        // Arrange
        var preferred = (ushort)TestPorts.GetFreeTcpPort();
        using var busy = new TcpListener(IPAddress.Any, preferred);
        busy.Start();

        // Act
        var port = PortProvider.AllocatePort(TransportType.Tcp, preferred, 5);

        // Assert
        port.Should().BeGreaterThan(preferred);
        PortProvider.ReleasePort(port, TransportType.Tcp);
    }

    /// <summary>
    /// TCP и UDP порты резервируются независимо
    /// </summary>
    [Fact]
    public void TcpAndUdpReservationsShouldBeIndependent()
    {
        // Arrange
        var preferred = (ushort)TestPorts.GetFreeTcpPort();

        // Act
        var tcpPort = PortProvider.AllocatePort(TransportType.Tcp, preferred, 1);
        var udpPort = PortProvider.AllocatePort(TransportType.Udp, preferred, 1);

        // Assert
        tcpPort.Should().Be(preferred);
        udpPort.Should().Be(preferred);
        PortProvider.ReleasePort(tcpPort, TransportType.Tcp);
        PortProvider.ReleasePort(udpPort, TransportType.Udp);
    }

    /// <summary>
    /// Предпочтительный порт у верхней границы не выходит за 65535 даже когда все порты диапазона заняты
    /// </summary>
    [Fact]
    public void AllocatePortNearUpperBoundShouldNotOverflow()
    {
        // Arrange
        var reserved = new List<int>();
        for (var candidate = 65530; candidate <= 65535; candidate++)
        {
            reserved.Add(PortProvider.AllocatePort(TransportType.Tcp, (ushort)candidate, 1));
        }

        // Act
        var port = PortProvider.AllocatePort(TransportType.Tcp, 65530, 10);

        // Assert
        port.Should().BeInRange(49152, 65535);
        PortProvider.ReleasePort(port, TransportType.Tcp);
        foreach (var released in reserved)
        {
            PortProvider.ReleasePort(released, TransportType.Tcp);
        }
    }

    /// <summary>
    /// Порт 65535 допустим как предпочтительный
    /// </summary>
    [Fact]
    public void AllocatePortShouldAcceptMaxPreferredPort()
    {
        // Act
        var port = PortProvider.AllocatePort(TransportType.Tcp, 65535, 1);

        // Assert
        port.Should().Be(65535);
        PortProvider.ReleasePort(port, TransportType.Tcp);
    }
}
