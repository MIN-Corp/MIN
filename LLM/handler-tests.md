# Guideline: тесты обработчиков MIN

Для `Core/Handlers` и `Infrastructure/*/Handlers`.
Образцы: `MIN.Core.Handlers.Tests` (`CoreHandlerTests`, `AdvancedHandlerTests`,
`ChatHandlerTests`, `MessageDispatcherTests`).

## Доступ к internal-обработчикам

Обработчики — `internal sealed`. Не добавляй InternalsVisibleTo: резолвь их
через настоящий DI-контур `HandlerTestFactory`:

```csharp
private readonly HandlerTestFactory factory = new();

var handler = factory.GetHandler(MessageTypeTag.ChatTextMessage);
var result = await handler.HandleAsync(message, context);
```

`HandlerTestFactory` регистрирует обработчики Core и Chat через
`RegisterMultipleInterfacesAssignableFromAnchor<IMessageHandler, ICoreHandlerAnchor>`
(и `IChatHandlerAnchor`), зависимости — моки/фикстуры:
`Logger`, `EventBus`, `RoomStore`, `Transport` (InMemoryTransport),
`RoomConnections`, `Identity`, моки `MessageSender`, `Lifecycle`, `Router`,
`Version`, `Encryptor`, `Persistence`, `NetworkErrorHandler`.
Контекст: `factory.RoomFactory.GetOrCreateContext(roomId)` +
`TestMessageContext.Create(roomContext, selfId, connectionId, role)`.

## Семантика HandlerResult

- `Success(stopPropagation)`, `Failure(msg, critical, stopPropagation)`,
  `WithErrorHandled(msg)`, `WithResponse(msg)`, `WithEvent(evt)`.
- **События из `ResultEvent` публикует ДИСПЕТЧЕР, не обработчик.** В юнит-тесте
  обработчика проверяй `result.ResultEvent.Should().BeOfType<T>()`, а не шину.
  Исключения: `PingHandler` и `ChatHistoryClear` публикуют сами.
- Response проверяй `result.Response.Should().BeOfType<T>().Subject`.
- Ошибки: `result.ErrorMessage.Should().NotBeNull()` / `Contain("кусок")`.

## Пример

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

## Тесты диспетчера

`MessageDispatcher` тестируется фейковыми обработчиками (`FakeHandler` с
настраиваемым результатом и счётчиком вызовов): приоритеты, StopPropagation
у Success/Failure/WithErrorHandled, исключения в обработчике, рекурсивная
диспетчеризация локальных Response, host-рассылка (broadcast один раз),
`MarkDirty`. Моки `IMessageSender` проверяй `Verify(..., Times.Once)`.

## Грабли

- `PingMessage.IsPublic => false` — для broadcast-тестов бери `ChatTextMessage`.
- Ответ `ChatHistoryResponse` приходит «новые первыми»; prepend в обработчике
  разворачивает в хронологию.
- `ReplyToMessageDescription` = `"SenderName: Content"` (IDescribable).
- Роли: `Role.Host` / `Role.Client`; хост валидирует право редактирования/
  удаления, клиент доверяет хосту.
- Для крипто-зависимых обработчиков (Handshake) настраивай
  `factory.Encryptor.Setup(...)` (GetLocalPublicKey, ComputeKeyFingerprint,
  TryInitializeSessionFromStoredAsync...).
