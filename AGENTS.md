# AGENTS.md

## Project Overview

MIN — распределённый мессенджер (.NET 8, Avalonia UI): комнаты, E2E-шифрование
(ECDH P-256 + HKDF → AES-256-GCM), TCP/UDP-транспорт, потоковая передача
чанками, UDP-broadcast discovery, файловые хранилища с версионированием схемы.
Solution: `MIN.sln`.

## Build & Test Commands

```bash
dotnet build MIN.sln

# Один тестовый проект (проекты лежат рядом с тестируемыми модулями)
dotnet test Core/Events/MIN.Core.Events.Tests/MIN.Core.Events.Tests.csproj

# Все тесты
find Core Infrastructure Helpers Common -name '*.Tests.csproj' -not -path '*/obj/*' | while read p; do dotnet test "$p"; done

# Один тест
dotnet test <csproj> --filter "FullyQualifiedName~MessageEncryptorTests.EncryptDecryptShouldRoundTrip"
```

**Important**: `TreatWarningsAsErrors` + `GenerateDocumentationFile` — сборка
упадёт на любом предупреждении и на public-членах без XML-доков.
Тестовые проекты (`*.Tests`) автоматически получают xunit, FluentAssertions,
Moq, Test.Sdk и RID `win-x64` из `Directory.Build.props` ( RID нужен, чтобы
не копировать нативы onnx/openh264 всех платформ в каждый bin).

## Architecture

- **Core/**: Cryptography, Messaging (контракты сообщений + MessageTypeTag),
  Events (InMemoryEventBus), Handlers (диспетчер + обработчики), Headers,
  Serialization (JsonMessageSerializer + конвертеры IEndpoint/IMessage),
  Transport (TcpSockets/UdpSockets/ChannelTransport), Streaming
  (StreamManager/ChunkBufferAssembler/MessageStream), Stores (сторы, реестры,
  RoomFactory, персистентность), Services (Messaging/Pipeline/Moderation/
  Persistence/Lifecycle), Protocol (рукопожатия), Identity, Entities, DI.
- **Infrastructure/**: Chat, Discovery, FileTransfer, Sessions, Voice,
  ScreenDemo, Audio — каждый с Messaging/Handlers/Services/DI.
- **Common/**: MIN.Common.Core (расширения), MIN.Common.Mvc (Module +
  DI-расширения), **MIN.Tests.Common** (общие тестовые фикстуры).
- **Helpers/**: логгер, настройки, версии, SchemaFileStore.
- **Desktop/**: Avalonia UI (тестами не покрыт).

## Testing

Тестовые проекты `MIN.*.Tests` расположены **рядом с тестируемым модулем**
(например `Infrastructure/Chat/MIN.Chat.Tests`), в `MIN.sln` сгруппированы
в виртуальную папку `Tests`.

- Скилл-роутер: `.agents/skills/unit-tests-writing/SKILL.md`
- Guidelines по типам тестов: `LLM/core-tests.md`, `LLM/handler-tests.md`,
  `LLM/service-tests.md`, `LLM/integration-tests.md`
- Каталог фикстур: `LLM/fixtures.md`

Конвенции: `{Subject}Tests` / `{Method}Should{Behavior}`, Arrange/Act/Assert,
русские XML-доки, CRLF + UTF-8 BOM. Обработчики internal — тестируются через
DI-контур (`HandlerTestFactory`), без InternalsVisibleTo.

## Conventions

- Файлы: CRLF, UTF-8 BOM, file-scoped namespaces, 4 пробела, `var`.
- Большинство публичных классов реализуют интерфейсы явно (explicit) —
  в тестах используй интерфейсные типы переменных.
- Исключения сторов: `*NotRegisteredException`.
- Текущая версия: см. `Directory.Build.props` (`Version`).
