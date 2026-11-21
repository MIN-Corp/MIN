# AGENTS.md

## Project Overview

MIN — a distributed messenger (.NET 8, Avalonia UI): rooms, E2E encryption
(ECDH P-256 + HKDF → AES-256-GCM), TCP/UDP transport, chunked streaming,
UDP-broadcast discovery, file stores with schema versioning.
Solution: `MIN.sln`.

## Build & Test Commands

```bash
dotnet build MIN.sln

# Single test project (projects live next to the tested modules)
dotnet test Core/Events/MIN.Core.Events.Tests/MIN.Core.Events.Tests.csproj

# All tests
find Core Infrastructure Helpers Common -name '*.Tests.csproj' -not -path '*/obj/*' | while read p; do dotnet test "$p"; done

# Single test
dotnet test <csproj> --filter "FullyQualifiedName~MessageEncryptorTests.EncryptDecryptShouldRoundTrip"
```

**Important**: `TreatWarningsAsErrors` + `GenerateDocumentationFile` — the
build fails on any warning and on public members without XML docs.
Test projects (`*.Tests`) automatically get xunit, FluentAssertions,
Moq, Test.Sdk and RID `win-x64` from `Directory.Build.props` ( the RID is
needed so that onnx/openh264 natives of all platforms are not copied into
every bin).

## Architecture

- **Core/**: Cryptography, Messaging (message contracts + MessageTypeTag),
  Events (InMemoryEventBus), Handlers (dispatcher + handlers), Headers,
  Serialization (JsonMessageSerializer + IEndpoint/IMessage converters),
  Transport (TcpSockets/UdpSockets/ChannelTransport), Streaming
  (StreamManager/ChunkBufferAssembler/MessageStream), Stores (stores, registries,
  RoomFactory, persistence), Services (Messaging/Pipeline/Moderation/
  Persistence/Lifecycle), Protocol (handshakes), Identity, Entities, DI.
- **Infrastructure/**: Chat, Discovery, FileTransfer, Sessions, Voice,
  ScreenDemo, Audio — each with Messaging/Handlers/Services/DI.
- **Common/**: MIN.Common.Core (extensions), MIN.Common.Mvc (Module +
  DI extensions), **MIN.Tests.Common** (shared test fixtures).
- **Helpers/**: logger, settings, versions, SchemaFileStore.
- **Desktop/**: Avalonia UI (not covered by tests).

## Testing

Test projects `MIN.*.Tests` are located **next to the tested module**
(e.g. `Infrastructure/Chat/MIN.Chat.Tests`); in `MIN.sln` they are grouped
in the same virtual solution folders as their tested modules.

- Skill router: `.agents/skills/unit-tests-writing/SKILL.md`
- Guidelines by test type: `LLM/core-tests.md`, `LLM/handler-tests.md`,
  `LLM/service-tests.md`, `LLM/integration-tests.md`
- Fixture catalog: `LLM/fixtures.md`

Conventions: `{Subject}Tests` / `{Method}Should{Behavior}`, Arrange/Act/Assert,
Russian XML docs, CRLF + UTF-8 BOM. Handlers are internal — tested through the
DI wiring (`HandlerTestFactory`), without InternalsVisibleTo.

## Conventions

- Files: CRLF, UTF-8 BOM, file-scoped namespaces, 4-space indent, `var`.
- Most public classes implement their interfaces explicitly — in tests use
  interface types for variables.
- Store exceptions: `*NotRegisteredException`.
- Current version: see `Directory.Build.props` (`Version`).
