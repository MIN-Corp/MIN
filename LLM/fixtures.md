# Каталог фикстур MIN.Tests.Common

Все фикстуры живут в `Common/MIN.Tests.Common` (namespace `MIN.Tests.Common`),
кроме отмеченных отдельно. Подключаются через ProjectReference; xunit,
FluentAssertions, Moq и Test.Sdk тестовые проекты получают автоматически из
`Directory.Build.props` (там же RID `win-x64`, чтобы не копировать нативы
всех платформ).

## Логирование и окружение

### TestLoggerProvider
`ILoggerProvider`, копит записи в памяти.
- `Items` — снапшот всех записей; `HasErrors` — есть ли ошибки.
- Событие `OnLogReceived`, метод `GetRecentLogHistory(page, pageSize)`.
- Используй для проверок «ошибка залогирована» вместо моков.

### TemporaryDirectory
`IDisposable`-директория в `Path.GetTempPath()`, удаляется при Dispose.
`Path` — путь. База для файловых тестов.

### TestIdentityDataPathProvider
`IIdentityDataPathProvider` поверх временной директории:
`CryptographyDirectory`, `RoomsDirectory`, `FilesDirectory`.
Для тестов ключей, RoomFileEncryptor, FileStorageService, RoomFileStore.

### TestAppDataProvider
`IAppDataProvider`: `VersionedDirectory` = `SharedDirectory` = временная
директория. Для IdentityFileStore, FileSystemSettingsStorage.

### TestIdentityService
`IIdentityService` с настраиваемым `SelfParticipant` (по умолчанию
`Participant { Name = "TestUser" }`) и вызываемым `SelfParticipantSaved`.

## Комнаты и контексты

### TestRoomFactory
Настоящий `RoomFactory` с DI-регистрациями сторов
(`MessageStore`, `ParticipantStore`, `ParticipantConnectionRegistry`, `SubRoomManager`).
- `Factory` — `IRoomFactory`; `CreateContext(roomId?)` — новый `RoomContext`.
- Dispose освобождает контексты и провайдер.

### TestMessageContext
`Create(roomContext, selfId, connectionId, role, ct)` — обёртка над ctor
`MessageContext`. **Порядок: selfId ПЕРЕД connectionId.**

### EncryptorPair
`await EncryptorPair.CreateAsync()` — два `MessageEncryptor` (Alice/Bob) с
выполненным ECDH-обменом. `Alice`/`Bob` — `IMessageEncryptor`,
`AliceId`/`BobId` — идентификаторы партнёров. Шифруешь у Alice для BobId —
расшифровывается у Bob с AliceId. Dispose освобождает оба шифратора.

## Сериализация

### TestSerializer
- `Create(params IMessage[])` — `JsonMessageSerializer` с указанными типами
  или со всеми известными (`AllMessages`).
- `AllMessages` — экземпляры всех конкретных `IMessage` якорных сборок.
- `AnchorAssemblies` — список сборок-якорей; по умолчанию лёгкие
  (Core, Chat, Discovery, FileTransfer, Sessions).
- `RegisterAnchorAssembly(assembly)` — добавить тяжёлый якорь (Voice,
  ScreenDemo) **до первого обращения к AllMessages**. В
  `MIN.Core.Serialization.Tests` это делает `[ModuleInitializer]`
  (`HeavyAnchorRegistration.cs`) — только этот проект тащит нативы
  Voice/ScreenDemo.

## Асинхронные ожидания

### TestWait
`UntilAsync(Func<bool> condition, timeoutMs = 5000, pollMs = 20)` — поллинг
условия с таймаутом. Для событий, которые приходят асинхронно.

### EventCapture<T>
`new EventCapture<T>(eventBus)` — подписывается и копит события.
- `Events` — снапшот; `WaitAsync(count = 1, timeoutMs = 5000)`.
- Dispose отписывается. Используй вместо ручных TCS.

## Транспорт

### InMemoryTransport
`ITransport` в памяти:
- `SentDatagrams` — очередь `SentDatagram(Data, RecipientConnectionId)`.
- `SendHandler` — `Func<SentDatagram, Task>`: маршрутизируй байты получателю
  в том же тесте (так устроен сквозной стенд стриминга).
- `Endpoints` — что возвращают `GetEndpoints`/`SetUpEndpoints`.
- `Connections` — очередь подключений; `BroadcastAsync` веерит по ним
  с учётом exclude.
- `Deliver(data, connectionId, serverConnectionId?)` — эмуляция входящих.
- `RaiseConnectionStateChanged(args)` — эмуляция событий соединения.
- `SendAsync` уважает CancellationToken.

### TestPorts (живёт в MIN.Core.Transport.Tests)
`GetFreeTcpPort()` / `GetFreeUdpPort()` — свободный порт на loopback
(биндим :0, читаем, отпускаем).

## Фикстуры внутри тестовых проектов

- `HandlerTestFactory` (MIN.Core.Handlers.Tests) — DI-контейнер с настоящими
  обработчиками Core/Chat и моками зависимостей; `GetHandler(tag)`.
  См. `LLM/handler-tests.md`.
- `CapturingDispatcher` (MIN.Core.Services.Tests) — `IMessageDispatcher`,
  копит `(Message, Context)` в `Dispatched`.
- `FakeDiscoveryTransport` / `CapturingResponder` (MIN.Discovery.Tests) —
  транспорт обнаружения в памяти + ответчик, копящий ответы.
