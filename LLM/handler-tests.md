# Guideline: handler tests for MIN

For `Core/Handlers` and `Infrastructure/*/Handlers`.
Reference projects: `MIN.Core.Handlers.Tests` (`CoreHandlerTests`, `AdvancedHandlerTests`,
`ChatHandlerTests`, `MessageDispatcherTests`).

## Accessing internal handlers

Handlers are `internal sealed`. Do not add InternalsVisibleTo: resolve them
through the real DI wiring of `HandlerTestFactory`:

```csharp
private readonly HandlerTestFactory factory = new();

var handler = factory.GetHandler(MessageTypeTag.ChatTextMessage);
var result = await handler.HandleAsync(message, context);
```

`HandlerTestFactory` registers Core and Chat handlers via
`RegisterMultipleInterfacesAssignableFromAnchor<IMessageHandler, ICoreHandlerAnchor>`
(and `IChatHandlerAnchor`); dependencies are mocks/fixtures:
`Logger`, `EventBus`, `RoomStore`, `Transport` (InMemoryTransport),
`RoomConnections`, `Identity`, and mocks of `MessageSender`, `Lifecycle`, `Router`,
`Version`, `Encryptor`, `Persistence`, `NetworkErrorHandler`.
Context: `factory.RoomFactory.GetOrCreateContext(roomId)` +
`TestMessageContext.Create(roomContext, selfId, connectionId, role)`.

## HandlerResult semantics

- `Success(stopPropagation)`, `Failure(msg, critical, stopPropagation)`,
  `WithErrorHandled(msg)`, `WithResponse(msg)`, `WithEvent(evt)`.
- **Events from `ResultEvent` are published by the DISPATCHER, not the handler.**
  In a handler unit test assert `result.ResultEvent.Should().BeOfType<T>()`,
  not the bus. Exceptions: `PingHandler` and `ChatHistoryClear` publish
  themselves.
- Response: `result.Response.Should().BeOfType<T>().Subject`.
- Errors: `result.ErrorMessage.Should().NotBeNull()` / `Contain("fragment")`.

## Example

```csharp
/// <summary>
/// Публичное текстовое сообщение добавляется в историю и порождает событие
/// </summary>
[Fact]
public async Task PublicChatTextShouldBeStoredAndNotified()
{
    // Arrange
    var senderId = Guid.NewGuid();
    roomContext.Participants.AddParticipant(new Participant { Id = senderId, Name = "Отправитель" });
    var handler = factory.GetHandler(MessageTypeTag.ChatTextMessage);
    var message = new ChatTextMessage { Content = "привет", SenderId = senderId, SenderName = "Отправитель" };

    // Act
    var result = await handler.HandleAsync(message, CreateContext());

    // Assert
    result.IsSuccess.Should().BeTrue();
    roomContext.Messages.GetMessageById(message.Id).Should().BeSameAs(message);
    var receivedEvent = result.ResultEvent.Should().BeOfType<ChatTextMessageReceivedEvent>().Subject;
    receivedEvent.Sender.Id.Should().Be(senderId);
}
```

## Dispatcher tests

`MessageDispatcher` is tested with fake handlers (`FakeHandler` with a
configurable result and a call counter): priorities, StopPropagation on
Success/Failure/WithErrorHandled, handler exceptions, recursive dispatch of
local Responses, host broadcast (sent once), `MarkDirty`. Assert mocks of
`IMessageSender` with `Verify(..., Times.Once)`.

## Pitfalls

- `PingMessage.IsPublic => false` — use `ChatTextMessage` for broadcast tests.
- `ChatHistoryResponse` arrives "newest first"; the handler's prepend flips it
  back into chronological order.
- `ReplyToMessageDescription` = `"SenderName: Content"` (IDescribable).
- Roles: `Role.Host` / `Role.Client`; the host validates edit/delete rights,
  the client trusts the host.
- For crypto-dependent handlers (Handshake) set up
  `factory.Encryptor.Setup(...)` (GetLocalPublicKey, ComputeKeyFingerprint,
  TryInitializeSessionFromStoredAsync...).
