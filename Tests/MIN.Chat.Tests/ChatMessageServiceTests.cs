using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Chat.Services;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.History;
using MIN.Core.Messaging.Stateless.RoomRelated.Messages;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Registries;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Chat.Tests;

/// <summary>
/// Тесты на <see cref="ChatMessageService"/>
/// </summary>
public class ChatMessageServiceTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly IRoomConnectionRegistry registry = new RoomConnectionRegistry();
    private readonly Mock<IMessageRouter> router = new();
    private readonly TestIdentityService identity = new();
    private readonly IChatMessageService service;
    private readonly Guid roomId = Guid.NewGuid();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatMessageServiceTests"/>
    /// </summary>
    public ChatMessageServiceTests()
    {
        service = new ChatMessageService(rooms.Factory, registry, router.Object, identity);
    }

    /// <summary>
    /// Редактирование меняет контент и маршрутизирует MessageUpdate
    /// </summary>
    [Fact]
    public async Task EditShouldUpdateContentAndRouteUpdate()
    {
        // Arrange
        var context = rooms.Factory.GetOrCreateContext(roomId);
        var original = new ChatTextMessage { Content = "старый" };
        context.Messages.AddMessage(original);

        // Act
        await service.EditTextMessageAsync(roomId, original.Id, "новый", CancellationToken.None);

        // Assert
        original.Content.Should().Be("новый");
        router.Verify(r => r.RouteAsync(
            It.Is<MessageUpdateMessage>(m => m.MessageIdToEdit == original.Id),
            roomId, identity.SelfParticipant.Id, It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Редактирование в неизвестной комнате отклоняется
    /// </summary>
    [Fact]
    public async Task EditUnknownRoomShouldThrow()
    {
        // Act
        var act = () => service.EditTextMessageAsync(Guid.NewGuid(), Guid.NewGuid(), "текст", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Комната не нашлась*");
    }

    /// <summary>
    /// Редактирование несуществующего сообщения отклоняется
    /// </summary>
    [Fact]
    public async Task EditUnknownMessageShouldThrow()
    {
        // Arrange
        rooms.Factory.GetOrCreateContext(roomId);

        // Act
        var act = () => service.EditTextMessageAsync(roomId, Guid.NewGuid(), "текст", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Сообщение не найдено*");
    }

    /// <summary>
    /// Удаление маршрутизирует MessageDelete
    /// </summary>
    [Fact]
    public async Task DeleteShouldRouteDelete()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        // Act
        await service.DeleteMessageAsync(roomId, messageId, CancellationToken.None);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.Is<MessageDeleteMessage>(m => m.MessageIdToDelete == messageId),
            roomId, identity.SelfParticipant.Id, It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Очистка истории на хосте маршрутизирует ChatHistoryClear
    /// </summary>
    [Fact]
    public async Task ClearWhenHostingShouldRouteClear()
    {
        // Arrange
        registry.RegisterServerConnection(roomId, Guid.NewGuid());
        var context = rooms.Factory.GetOrCreateContext(roomId);
        context.Messages.AddMessage(new ChatTextMessage { Content = "сообщение" });

        // Act
        await service.ClearMessageHistoryAsync(roomId, CancellationToken.None);

        // Assert
        router.Verify(r => r.RouteAsync(
            It.IsAny<ChatHistoryClearMessage>(),
            roomId, identity.SelfParticipant.Id, It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once);
    }

    /// <summary>
    /// Очистка истории на клиенте публикуется локально
    /// </summary>
    [Fact]
    public async Task ClearWhenClientShouldPublishLocally()
    {
        // Arrange
        registry.RegisterClientConnection(roomId, Guid.NewGuid());
        var context = rooms.Factory.GetOrCreateContext(roomId);
        context.Messages.AddMessage(new ChatTextMessage { Content = "сообщение" });

        // Act
        await service.ClearMessageHistoryAsync(roomId, CancellationToken.None);

        // Assert
        router.Verify(r => r.PublishLocally(
            It.IsAny<ChatHistoryClearMessage>(),
            roomId, It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Очистка пустой истории отклоняется
    /// </summary>
    [Fact]
    public async Task ClearEmptyHistoryShouldThrow()
    {
        // Arrange
        rooms.Factory.GetOrCreateContext(roomId);

        // Act
        var act = () => service.ClearMessageHistoryAsync(roomId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*История уже очищена*");
    }

    /// <inheritdoc />
    public void Dispose() => rooms.Dispose();
}
