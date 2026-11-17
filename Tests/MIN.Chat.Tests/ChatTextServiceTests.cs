using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Chat.Services;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Entities;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Chat.Tests;

/// <summary>
/// Тесты на <see cref="ChatTextService"/>
/// </summary>
public class ChatTextServiceTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly Mock<IMessageRouter> router = new();
    private readonly TestIdentityService identity = new();
    private readonly IChatTextService service;
    private readonly Guid roomId = Guid.NewGuid();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatTextServiceTests"/>
    /// </summary>
    public ChatTextServiceTests()
    {
        identity.SelfParticipant = new Participant { Name = "Автор" };
        service = new ChatTextService(router.Object, rooms.Factory, identity);
    }

    /// <summary>
    /// Текст маршрутизируется с контентом и именем отправителя
    /// </summary>
    [Fact]
    public async Task SendShouldRouteMessageWithContentAndSender()
    {
        // Act
        await service.SendTextMessageAsync(roomId, "привет", null, null, CancellationToken.None);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<ChatTextMessage>(m => m.Content == "привет" && m.SenderName == "Автор" && m.RecipientId == null),
            roomId, identity.SelfParticipant.Id, It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Пустой и пробельный контент отклоняется
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task EmptyContentShouldThrow(string? content)
    {
        // Act
        var act = () => service.SendTextMessageAsync(roomId, content!, null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Приватное сообщение несёт получателя
    /// </summary>
    [Fact]
    public async Task PrivateMessageShouldCarryRecipient()
    {
        // Arrange
        var recipientId = Guid.NewGuid();

        // Act
        await service.SendTextMessageAsync(roomId, "лично", recipientId, null, CancellationToken.None);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<ChatTextMessage>(m => m.RecipientId == recipientId),
            roomId, It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Ответ на существующее сообщение несёт его описание
    /// </summary>
    [Fact]
    public async Task ReplyToExistingShouldCarryDescription()
    {
        // Arrange
        var original = new ChatTextMessage { Content = "оригинал", SenderName = "Кто-то" };
        rooms.Factory.GetOrCreateContext(roomId).Messages.AddMessage(original);

        // Act
        await service.SendTextMessageAsync(roomId, "ответ", null, original.Id, CancellationToken.None);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<ChatTextMessage>(m => m.ReplyToMessageId == original.Id && m.ReplyToMessageDescription == "Кто-то: оригинал"),
            roomId, It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Ответ на несуществующее сообщение сохраняет id без описания
    /// </summary>
    [Fact]
    public async Task ReplyToMissingShouldKeepIdWithoutDescription()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act
        await service.SendTextMessageAsync(roomId, "ответ", null, missingId, CancellationToken.None);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<ChatTextMessage>(m => m.ReplyToMessageId == missingId && m.ReplyToMessageDescription == null),
            roomId, It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <inheritdoc />
    public void Dispose() => rooms.Dispose();
}
