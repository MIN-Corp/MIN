# Catalog of MIN.Tests.Common fixtures

All fixtures live in `Common/MIN.Tests.Common` (namespace `MIN.Tests.Common`),
except those noted separately. Connected via ProjectReference; xunit,
FluentAssertions, Moq and Test.Sdk are pulled into test projects automatically
from `Directory.Build.props` (same place sets RID `win-x64` so native binaries
of all platforms are not copied into every bin).

## Logging and environment

### TestLoggerProvider
`ILoggerProvider`, accumulates entries in memory.
- `Items` — snapshot of all entries; `HasErrors` — whether any errors exist.
- Event `OnLogReceived`, method `GetRecentLogHistory(page, pageSize)`.
- Use it for "error was logged" checks instead of mocks.

### TemporaryDirectory
`IDisposable` directory under `Path.GetTempPath()`, removed on Dispose.
`Path` — the path. Base for file tests.

### TestIdentityDataPathProvider
`IIdentityDataPathProvider` on top of a temp directory:
`CryptographyDirectory`, `RoomsDirectory`, `FilesDirectory`.
For key tests, RoomFileEncryptor, FileStorageService, RoomFileStore.

### TestAppDataProvider
`IAppDataProvider`: `VersionedDirectory` = `SharedDirectory` = temp directory.
For IdentityFileStore, FileSystemSettingsStorage.

### TestIdentityService
`IIdentityService` with configurable `SelfParticipant` (defaults to
`Participant { Name = "TestUser" }`) and invocable `SelfParticipantSaved`.

## Rooms and contexts

### TestRoomFactory
A real `RoomFactory` with DI registrations of the stores
(`MessageStore`, `ParticipantStore`, `ParticipantConnectionRegistry`, `SubRoomManager`).
- `Factory` — `IRoomFactory`; `CreateContext(roomId?)` — new `RoomContext`.
- Dispose releases the contexts and the provider.

### TestMessageContext
`Create(roomContext, selfId, connectionId, role, ct)` — wrapper over the
`MessageContext` ctor. **Order: selfId BEFORE connectionId.**

### EncryptorPair
`await EncryptorPair.CreateAsync()` — two `MessageEncryptor`s (Alice/Bob) with
a completed ECDH exchange. `Alice`/`Bob` are `IMessageEncryptor`,
`AliceId`/`BobId` are partner ids. Encrypt at Alice for BobId — decrypts at
Bob with AliceId. Dispose releases both encryptors.

## Serialization

### TestSerializer
- `Create(params IMessage[])` — `JsonMessageSerializer` with the given types
  or with all known ones (`AllMessages`).
- `AllMessages` — instances of every concrete `IMessage` from the anchor
  assemblies.
- `AnchorAssemblies` — list of anchor assemblies; the defaults are lightweight
  ones (Core, Chat, Discovery, FileTransfer, Sessions).
- `RegisterAnchorAssembly(assembly)` — add a heavy anchor (Voice,
  ScreenDemo) **before the first access to AllMessages**. In
  `MIN.Core.Serialization.Tests` a `[ModuleInitializer]` does this
  (`HeavyAnchorRegistration.cs`) — only that project pulls in Voice/ScreenDemo
  natives.

## Asynchronous waiting

### TestWait
`UntilAsync(Func<bool> condition, timeoutMs = 5000, pollMs = 20)` — polls a
condition with a timeout. For events that arrive asynchronously.

### EventCapture<T>
`new EventCapture<T>(eventBus)` — subscribes and accumulates events.
- `Events` — snapshot; `WaitAsync(count = 1, timeoutMs = 5000)`.
- Dispose unsubscribes. Use it instead of manual TCS.

## Transport

### InMemoryTransport
In-memory `ITransport`:
- `SentDatagrams` — queue of `SentDatagram(Data, RecipientConnectionId)`.
- `SendHandler` — `Func<SentDatagram, Task>`: route the bytes to the recipient
  within the same test (this is how the end-to-end streaming stand works).
- `Endpoints` — what `GetEndpoints`/`SetUpEndpoints` return.
- `Connections` — queue of connections; `BroadcastAsync` fans out over them
  honoring exclude.
- `Deliver(data, connectionId, serverConnectionId?)` — emulates incoming data.
- `RaiseConnectionStateChanged(args)` — emulates connection events.
- `SendAsync` honors the CancellationToken.

### TestPorts (lives in MIN.Core.Transport.Tests)
`GetFreeTcpPort()` / `GetFreeUdpPort()` — a free loopback port
(bind :0, read it, release it).

## Fixtures inside test projects

- `HandlerTestFactory` (MIN.Core.Handlers.Tests) — DI container with real
  Core/Chat handlers and mocked dependencies; `GetHandler(tag)`.
  See `LLM/handler-tests.md`.
- `CapturingDispatcher` (MIN.Core.Services.Tests) — `IMessageDispatcher`
  that accumulates `(Message, Context)` in `Dispatched`.
- `FakeDiscoveryTransport` / `CapturingResponder` (MIN.Discovery.Tests) —
  in-memory discovery transport + a responder accumulating replies.
