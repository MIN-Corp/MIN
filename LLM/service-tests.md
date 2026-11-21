# Guideline: service tests for MIN

For `Core/Services*` and `Infrastructure/*/Services`.
Reference projects: `MIN.Core.Services.Tests` (MessageRouter, NetworkErrorHandler,
RoomPersistenceService), `MIN.Chat.Tests` (ChatTextService, ChatMessageService,
ChatRoomService, ChatStatusService).

## Setup

A service is constructed directly: real lightweight dependencies + mocks at
external boundaries (`IMessageRouter`, `IMessageSender`, `IRoomLifecycleManager`,
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

- Keep services with explicit interface implementations in a field typed as
  the interface (`IChatTextService service = new ChatTextService(...)`).
- For pipeline tests use `CapturingDispatcher` (accumulates dispatched
  messages) instead of a mock — it is easier to assert the context.

## Checks

- Routing: `router.Verify(r => r.RouteAsync(It.Is<ChatTextMessage>(m =>
  m.Content == "привет" && m.SenderName == "Автор"), roomId, identity.SelfParticipant.Id,
  It.IsAny<CancellationToken>(), It.IsAny<IEnumerable<Guid>?>()), Times.Once)`.
- Negative: `await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Комната не нашлась*")`.
- Events: real `InMemoryEventBus` + `EventCapture<T>` + `TestWait.UntilAsync`.
- Async event handlers (async void): wait for the effect via
  `TestWait.UntilAsync(() => mock.Invocations.Any(...))`, then `Verify`.
- Persistence: `Mock<IRoomFileStore>` — set `LoadAll()` up explicitly
  (otherwise null → NRE in foreach); publish `RoomWentOfflineEvent` /
  `RoomDestroyedEvent` to a real bus.

## Pitfalls

- `MessageRouter`: host → `PublishLocally` (Host role), client → `SendAsync`
  to the host; unknown room → `RoomNotRegisteredException`; client without a
  host connection → `InvalidOperationException`.
- `NetworkErrorHandler`: critical error → `DisconnectMessage` + timer;
  ACK (`DisconnectAckReceived`) cancels the timer and kicks. Do timeout tests
  with `timeoutMs: 100`, not 5000.
- `ChatRoomService.KickParticipantAsync`: not host → throw; offline participant
  → `KickClientAsync` directly; online → critical error via errorHandler.
- Module DI smoke: `new ChatModule().Configure(services)` + mocked
  dependencies, resolve only the needed services. A container with
  IAsyncDisposable-only services must be released via `DisposeAsync`
  (test class implements `IAsyncDisposable`).
- DI extensions do not register nested (nested) interfaces (`IsPublic`
  filter) — put test interfaces at the top level.
