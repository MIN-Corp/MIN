using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Chat.Services;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Chat.Tests;

/// <summary>
/// Тесты на <see cref="ChatStatusService"/>
/// </summary>
public class ChatStatusServiceTests
{
    /// <summary>
    /// Смена статуса маршрутизирует OnlineStatusChanged
    /// </summary>
    [Fact]
    public async Task StatusShouldRouteOnlineStatusChanged()
    {
        // Arrange
        var router = new Mock<IMessageRouter>();
        var identity = new TestIdentityService();
        IChatStatusService service = new ChatStatusService(router.Object, identity);
        var roomId = Guid.NewGuid();

        // Act
        await service.SendSelfOnlineStatusChangedAsync(roomId, OnlineStatus.Offline, CancellationToken.None);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<OnlineStatusChangedMessage>(m => m.Status == OnlineStatus.Offline),
            roomId, identity.SelfParticipant.Id, It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }
}
