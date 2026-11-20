# Guideline: тесты сервисов MIN

Для `Core/Services*` и `Infrastructure/*/Services`.
Образцы: `MIN.Core.Services.Tests` (MessageRouter, NetworkErrorHandler,
RoomPersistenceService), `MIN.Chat.Tests` (ChatTextService, ChatMessageService,
ChatRoomService, ChatStatusService).

## Setup

Сервис создаётся напрямую: реальные лёгкие зависимости + моки внешних
границ (`IMessageRouter`, `IMessageSender`, `IRoomLifecycleManager`,
`IDiscoveryService`, `IRoomFileStore`...):

```csharp
public class ChatTextServiceTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly Mock<IMessageRouter> router = new();
    private readonly TestIdentityService identity = new();
    private readonly IChatTextService service;
    private readonly Guid roomId = Guid.NewGuid();

    public ChatTextServiceTests()
    {
        identity.SelfParticipant = new Participant { Name = "Автор" };
        service = new ChatTextService(router.Object, rooms.Factory, identity);
    }

    public void Dispose() => rooms.Dispose();
}
```

- Сервисы с явными реализациями интерфейсов держи в поле типом интерфейса
  (`IChatTextService service = new ChatTextService(...)`).
- Для pipeline-тестов используй `CapturingDispatcher` (копит диспетчеризованные
  сообщения) вместо мока — удобнее проверять контекст.

## Проверки

- Маршрутизация: `router.Verify(r => r.RouteAsync(It.Is<ChatTextMessage>(m =>
  m.Content == "привет" && m.SenderName == "Автор"), roomId, identity.SelfParticipant.Id,
  It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once)`.
- Негатив: `await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Комната не нашлась*")`.
- События: реальный `InMemoryEventBus` + `EventCapture<T>` + `TestWait.UntilAsync`.
- Асинхронные обработчики событий (async void): жди эффект через
  `TestWait.UntilAsync(() => mock.Invocations.Any(...))`, затем `Verify`.
- Персистентность: `Mock<IRoomFileStore>` — `LoadAll()` настрой явно
  (иначе null → NRE в foreach); события `RoomWentOfflineEvent` /
  `RoomDestroyedEvent` публикуй в реальную шину.

## Грабли

- `MessageRouter`: хост → `PublishLocally` (роль Host), клиент → `SendAsync`
  хосту; неизвестная комната → `RoomNotRegisteredException`; клиент без
  соединения хоста → `InvalidOperationException`.
- `NetworkErrorHandler`: критичная ошибка → `DisconnectMessage` + таймер;
  ACK (`DisconnectAckReceived`) отменяет таймер и кикает. Таймаут-тесты делай
  с `timeoutMs: 100`, не 5000.
- `ChatRoomService.KickParticipantAsync`: не хост → throw; оффлайн-участник →
  `KickClientAsync` напрямую; онлайн → критичная ошибка через errorHandler.
- DI-смоук модулей: `new ChatModule().Configure(services)` + моки зависимостей,
  резолвь только нужные сервисы. Контейнер с IAsyncDisposable-only сервисами
  освобождай через `DisposeAsync` (тестовый класс — `IAsyncDisposable`).
- Вложенные (nested) интерфейсы DI-расширения не регистрируют (фильтр
  `IsPublic`) — тестовые интерфейсы выноси на верхний уровень.
